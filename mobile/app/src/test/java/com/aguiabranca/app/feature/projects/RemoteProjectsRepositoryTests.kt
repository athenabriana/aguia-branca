package com.aguiabranca.app.feature.projects

import app.cash.turbine.test
import com.aguiabranca.app.core.domain.ProjectInput
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.ProjectsApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.core.network.problemJson
import com.aguiabranca.app.feature.projects.data.RemoteProjectsRepository
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RemoteProjectsRepositoryTests {
    private val h = NetworkHarness()
    private val bus = RefreshBus()
    private val repo = RemoteProjectsRepository(h.api(ProjectsApi::class.java), bus)

    @After fun tearDown() = h.shutdown()

    private fun project(id: String = "p1", stage: String = "EM_EXECUCAO", version: Int = 2) =
        """{"id":"$id","title":"PROJ: Roteirização","description":"d","stage":"$stage","statusText":"ok","investment":120000,"targetDate":"2026-12-20T00:00:00Z",
        "financialReturn":310000.5,"productivityGain":12.5,"costReduction":45000,"division":"LOGISTICA","guidelineId":"g1","guidelineTitle":"Eficiência",
        "creatorManagerId":"m1","originatingIdeaId":"i1","priorityScore":648,"reporterId":"o1","reporterName":"Ana","responsibleId":"m1","responsibleName":"Gestor",
        "netProfit":190000.5,"roiPercent":158.33,"version":$version,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-02T10:00:00.123Z"}"""

    private fun page(vararg items: String) = """{"items":[${items.joinToString(",")}],"page":1,"pageSize":200,"totalItems":${items.size}}"""

    private fun input(stage: ProjectStage = ProjectStage.CONCLUIDO, version: Int? = 2) = ProjectInput(
        title = " Projeto ", description = "d", stage = stage, statusText = "Entregue", investment = 100000.0, targetDate = 1_797_724_800_000L,
        financialReturn = 300000.0, productivityGain = 12.5, costReduction = 40000.0, division = Division.LOGISTICA, guidelineId = "g1",
        responsibleId = "m2", responsibleName = "Outro", version = version
    )

    @Test fun `list maps money, dates, guideline title and the server version`() = runBlocking {
        h.server.enqueue(MockResponse().json(page(project())))

        val p = repo.observeAll().first().single()

        assertEquals(ProjectStage.EM_EXECUCAO, p.stage)
        assertEquals(310000.5, p.financialReturn, 0.0)
        assertEquals("Eficiência", p.guidelineTitle)
        assertEquals(2, p.version)
        assertEquals(648, p.priorityScore)
        assertEquals(1_797_724_800_000L, p.targetDate)   // 2026-12-20T00:00:00Z
        assertTrue("fração de segundos ISO é aceita", p.updatedAt % 1000 == 123L)
    }

    @Test fun `update is a full PUT carrying version and note but no editor identity`() = runBlocking {
        h.server.enqueue(MockResponse().json(project(stage = "CONCLUIDO", version = 3)))

        val outcome = repo.update("p1", input(), "EDITOR-ID", "Editor", "  Meta atingida ")

        assertEquals(Outcome.Success(Unit), outcome)
        val put = h.server.takeRequest()
        val body = put.body.readUtf8()
        assertEquals("PUT /api/v1/projects/p1", "${put.method} ${put.path}")
        assertTrue(body.contains("\"stage\":\"CONCLUIDO\"") && body.contains("\"version\":2") && body.contains("\"note\":\"Meta atingida\""))
        assertTrue(body.contains("\"targetDate\":\"2026-12-20T00:00:00Z\"") && body.contains("\"responsibleId\":\"m2\""))
        assertTrue("identidade do editor não vai no corpo", !body.contains("EDITOR-ID") && !body.contains("creatorManagerId"))
    }

    @Test fun `update of a stale version is a 409 with the other manager message`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(409, "CONCURRENCY_CONFLICT", "O projeto foi alterado por outra pessoa."), 409))

        val error = (repo.update("p1", input(version = 1), "m", "M", "") as Outcome.Failure).error

        assertTrue(error is DomainError.ConflictingState && error.message.contains("outro gestor"))
    }

    @Test fun `timeline diff comes typed and renders like before, with the deadline as millis`() = runBlocking {
        h.server.enqueue(MockResponse().json(page(
            """{"id":"u2","projectId":"p1","authorId":"m1","authorName":"Gestor","note":"Prazo","createdAt":"2026-09-05T10:00:00Z","changes":[
                {"field":"investment","from":0,"to":45000},
                {"field":"stage","from":"PLANEJAMENTO","to":"EM_EXECUCAO"},
                {"field":"targetDate","from":null,"to":"2026-12-20T00:00:00Z"}]}""",
            """{"id":"u1","projectId":"p1","authorId":"m1","authorName":"Gestor","note":"Criado","createdAt":"2026-09-01T10:00:00Z","changes":[]}"""
        )))

        val updates = repo.observeUpdates("p1").first()

        assertEquals(listOf("Prazo", "Criado"), updates.map { it.note })
        val (investment, stage, target) = updates[0].changes
        assertEquals(0.0, investment.from as Double, 0.0); assertEquals(45000.0, investment.to as Double, 0.0)
        assertEquals("EM_EXECUCAO", stage.to)
        assertNull(target.from); assertEquals(1_797_724_800_000L, target.to)
        assertTrue(updates[1].changes.isEmpty())
        assertEquals("/api/v1/projects/p1/updates?page=1&pageSize=200", h.server.takeRequest().path)
    }

    @Test fun `create posts the form and refreshes projects, reports, ideas and ranking`() = runBlocking {
        h.server.enqueue(MockResponse().json(project(id = "novo", version = 1), 201))

        bus.events.test {
            val outcome = repo.create(input(ProjectStage.PLANEJAMENTO, null), "m", "M", "ignorada")

            assertEquals(Outcome.Success("novo"), outcome)
            assertEquals(
                setOf(RefreshKeys.PROJECTS, RefreshKeys.REPORTS, RefreshKeys.IDEAS, RefreshKeys.RANKING),
                setOf(awaitItem(), awaitItem(), awaitItem(), awaitItem())
            )
            cancelAndIgnoreRemainingEvents()
        }
        val body = h.server.takeRequest().body.readUtf8()
        assertTrue(!body.contains("originatingIdeaId") && !body.contains("ignorada") && !body.contains("\"note\""))
    }

    @Test fun `a leader or operator forcing a write gets PermissionDenied`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(403, "FORBIDDEN", "Você não tem permissão para esta ação."), 403))
        h.server.enqueue(MockResponse().json(problemJson(403, "FORBIDDEN", "Você não tem permissão para esta ação."), 403))

        assertTrue((repo.update("p1", input(), "l", "L", "") as Outcome.Failure).error is DomainError.PermissionDenied)
        assertTrue((repo.delete("p1") as Outcome.Failure).error is DomainError.PermissionDenied)
    }

    @Test fun `delete uses DELETE and by-guideline filters on the server`() = runBlocking {
        h.server.enqueue(MockResponse().setResponseCode(204))
        h.server.enqueue(MockResponse().json(page(project())))

        assertEquals(Outcome.Success(Unit), repo.delete("p1"))
        assertEquals(1, repo.observeByGuideline("g1").first().size)

        assertEquals("DELETE /api/v1/projects/p1", h.server.takeRequest().let { "${it.method} ${it.path}" })
        assertEquals("/api/v1/projects?guidelineId=g1&page=1&pageSize=200", h.server.takeRequest().path)
    }
}
