package com.aguiabranca.app.feature.ideas

import app.cash.turbine.test
import com.aguiabranca.app.core.domain.CreateIdeaInput
import com.aguiabranca.app.core.domain.UpdateIdeaInput
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.usecase.ApproveIdeaUseCase
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.IdeasApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.core.network.problemJson
import com.aguiabranca.app.core.util.Analytics
import com.aguiabranca.app.feature.ideas.data.RemoteIdeasRepository
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RemoteIdeasRepositoryTests {
    private val h = NetworkHarness()
    private val bus = RefreshBus()
    private val api = h.api(IdeasApi::class.java)
    private val repo = RemoteIdeasRepository(api, bus)

    @After fun tearDown() = h.shutdown()

    private fun idea(
        id: String = "i1", status: String = "SUBMETIDA", guidelineId: String? = null, guidelineTitle: String? = null,
        linked: String? = null, points: Int? = null, ice: String = "null"
    ) = """{"id":"$id","title":"Ideia","description":"d","category":"Tecnologia","division":"LOGISTICA","guidelineId":${guidelineId?.let { "\"$it\"" } ?: "null"},
        "guidelineTitle":${guidelineTitle?.let { "\"$it\"" } ?: "null"},"authorId":"a1","authorName":"Ana","status":"$status","ice":$ice,"reviewerId":null,"reviewComment":null,
        "createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-02T10:00:00Z","reviewedAt":null,"linkedProject":${linked ?: "null"},"pointsAwarded":${points ?: "null"}}"""

    private fun page(vararg items: String) = """{"items":[${items.joinToString(",")}],"page":1,"pageSize":200,"totalItems":${items.size}}"""

    @Test fun `each observe uses the matching server scope`() = runBlocking {
        repeat(4) { h.server.enqueue(MockResponse().json(page())) }

        repo.observeByAuthor("qualquer").first()
        repo.observeForCuration().first()
        repo.observeAll().first()
        repo.observeByGuideline("g9").first()

        val paths = List(4) { h.server.takeRequest().path!! }
        assertEquals("/api/v1/ideas?scope=mine&page=1&pageSize=200", paths[0])
        assertEquals("/api/v1/ideas?scope=curation&page=1&pageSize=200", paths[1])
        assertEquals("/api/v1/ideas?scope=all&page=1&pageSize=200", paths[2])
        assertEquals("/api/v1/ideas?scope=all&guidelineId=g9&page=1&pageSize=200", paths[3])
    }

    @Test fun `mapping brings guideline title, ICE and the linked project for the stepper`() = runBlocking {
        h.server.enqueue(MockResponse().json(page(
            idea(status = "APROVADA", guidelineId = "g1", guidelineTitle = "Eficiência",
                linked = """{"id":"p1","stage":"EM_EXECUCAO","updatedAt":"2026-09-10T12:00:00Z"}""", ice = """{"impact":9,"confidence":9,"ease":8,"score":648}"""),
            idea(id = "i2", guidelineId = "g-removida", guidelineTitle = null)
        )))

        val (a, b) = repo.observeAll().first()

        assertEquals(IdeaStatus.APROVADA, a.status)
        assertEquals("Eficiência", a.guidelineTitle)
        assertEquals(648, a.ice?.score)
        assertEquals(ProjectStage.EM_EXECUCAO, a.linkedProject?.stage)
        assertTrue(a.linkedProject!!.updatedAt!! > 0)
        assertTrue("orientação removida = id sem título", b.guidelineId != null && b.guidelineTitle == null)
        assertNull(b.linkedProject)
    }

    @Test fun `create returns the points granted by the server and never sends author or status`() = runBlocking {
        h.server.enqueue(MockResponse().json(idea(guidelineId = "g1", guidelineTitle = "X", points = 15), 201))

        val outcome = repo.createIdea(CreateIdeaInput(" Título ", "desc", " tecnologia ", Division.LOGISTICA, "g1", "ANA-ID", "Ana"))

        assertEquals(15, (outcome as Outcome.Success).value.pointsAwarded)
        val body = h.server.takeRequest().body.readUtf8()
        assertTrue(body.contains("\"title\":\"Título\"") && body.contains("\"guidelineId\":\"g1\""))
        assertTrue(!body.contains("ANA-ID") && !body.contains("authorId") && !body.contains("status"))
    }

    @Test fun `create invalidates ideas, profile, ranking and reports so points show up immediately`() = runBlocking {
        h.server.enqueue(MockResponse().json(idea(points = 10), 201))

        bus.events.test {
            repo.createIdea(CreateIdeaInput("Título", "descrição", "Tec", Division.COMERCIO, null, "a", "A"))
            assertEquals(
                setOf(RefreshKeys.IDEAS, RefreshKeys.ME, RefreshKeys.RANKING, RefreshKeys.REPORTS),
                setOf(awaitItem(), awaitItem(), awaitItem(), awaitItem())
            )
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `save ICE validates locally then PUTs the three dimensions`() = runBlocking {
        val invalid = repo.saveIce("i1", Ice(0, 5, 5), "rev")
        assertTrue((invalid as Outcome.Failure).error is DomainError.ValidationFailed)
        assertEquals("nenhuma chamada para ICE inválido", 0, h.server.requestCount)

        h.server.enqueue(MockResponse().json(idea(status = "EM_ANALISE")))
        assertEquals(Outcome.Success(Unit), repo.saveIce("i1", Ice(9, 8, 7), "rev"))
        val put = h.server.takeRequest()
        assertEquals("PUT /api/v1/ideas/i1/ice", "${put.method} ${put.path}")
        assertEquals("""{"impact":9,"confidence":8,"ease":7}""", put.body.readUtf8())
    }

    @Test fun `reject requires a comment and posts it trimmed`() = runBlocking {
        assertTrue((repo.rejectIdea("i1", "rev", "   ") as Outcome.Failure).error is DomainError.ValidationFailed)
        h.server.enqueue(MockResponse().json(idea(status = "REJEITADA")))

        assertEquals(Outcome.Success(Unit), repo.rejectIdea("i1", "rev", "  Fora do escopo  "))

        val post = h.server.takeRequest()
        assertEquals("POST /api/v1/ideas/i1/reject", "${post.method} ${post.path}")
        assertEquals("""{"comment":"Fora do escopo"}""", post.body.readUtf8())
    }

    @Test fun `approve calls the server endpoint, logs analytics and refreshes ideas projects and reports`() = runBlocking {
        val analytics = mockk<Analytics>(relaxed = true)
        val approve = ApproveIdeaUseCase(api, analytics, bus)
        h.server.enqueue(MockResponse().json("""{"ideaId":"i1","projectId":"p77","alreadyApproved":false}"""))

        bus.events.test {
            val outcome = approve("i1", "rev", "Gestor")

            assertEquals(Outcome.Success("p77"), outcome)
            assertEquals(setOf(RefreshKeys.IDEAS, RefreshKeys.PROJECTS, RefreshKeys.REPORTS), setOf(awaitItem(), awaitItem(), awaitItem()))
            cancelAndIgnoreRemainingEvents()
        }
        verify(exactly = 1) { analytics.logIdeaApproved("p77") }
        assertEquals("POST /api/v1/ideas/i1/approve", h.server.takeRequest().let { "${it.method} ${it.path}" })
    }

    @Test fun `self approval comes back as the existing permission message and logs nothing`() = runBlocking {
        val analytics = mockk<Analytics>(relaxed = true)
        h.server.enqueue(MockResponse().json(problemJson(403, "SELF_APPROVAL_FORBIDDEN", "Você não pode aprovar a própria ideia."), 403))

        val outcome = ApproveIdeaUseCase(api, analytics, bus)("i1", "rev", "Gestor")

        assertEquals(DomainError.PermissionDenied("Você não pode aprovar a própria ideia."), (outcome as Outcome.Failure).error)
        verify(exactly = 0) { analytics.logIdeaApproved(any()) }
    }

    @Test fun `edit after the idea left SUBMETIDA is a conflict and delete of a missing idea is NotFound`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(409, "IDEA_NOT_EDITABLE", "A ideia só pode ser editada enquanto estiver SUBMETIDA."), 409))
        h.server.enqueue(MockResponse().json(problemJson(404, "RESOURCE_NOT_FOUND", "Ideia não encontrada."), 404))

        val edit = repo.updateIdea("i1", UpdateIdeaInput("T", "D", "C", Division.LOGISTICA, null))
        val delete = repo.deleteIdea("i2", "a")

        assertEquals(DomainError.ConflictingState("A ideia só pode ser editada enquanto estiver SUBMETIDA."), (edit as Outcome.Failure).error)
        assertTrue((delete as Outcome.Failure).error is DomainError.NotFound)
    }

    @Test fun `observe emits null for a missing idea`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(404, "RESOURCE_NOT_FOUND", "x"), 404))
        assertNull(repo.observe("nao-existe").first())
    }
}
