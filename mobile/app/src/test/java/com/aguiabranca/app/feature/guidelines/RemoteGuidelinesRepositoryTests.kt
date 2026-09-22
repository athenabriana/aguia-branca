package com.aguiabranca.app.feature.guidelines

import app.cash.turbine.test
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.api.GuidelinesApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.core.network.problemJson
import com.aguiabranca.app.feature.guidelines.data.RemoteGuidelinesRepository
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RemoteGuidelinesRepositoryTests {
    private val h = NetworkHarness()
    private val bus = RefreshBus()
    private val repo = RemoteGuidelinesRepository(h.api(GuidelinesApi::class.java), bus)

    @After fun tearDown() = h.shutdown()

    private fun page(vararg items: String, total: Int = items.size) =
        """{"items":[${items.joinToString(",")}],"page":1,"pageSize":200,"totalItems":$total}"""

    private fun g(id: String, title: String, campaign: String? = null, updated: String = "2026-09-21T12:00:00Z") =
        """{"id":"$id","title":"$title","description":"d","pillar":"IDEIAS","campaign":${campaign?.let { "\"$it\"" } ?: "null"},"authorId":"a","authorName":"Líder","createdAt":"2026-09-01T00:00:00Z","updatedAt":"$updated"}"""

    @Test fun `observeAll maps the list including the optional campaign`() = runBlocking {
        h.server.enqueue(MockResponse().json(page(g("1", "Eficiência", "Campanha 2026"), g("2", "Cliente"))))

        val list = repo.observeAll().first()

        assertEquals(listOf("Eficiência", "Cliente"), list.map { it.title })
        assertEquals("Campanha 2026", list[0].campaign)
        assertNull(list[1].campaign)
        assertEquals(Pillar.IDEIAS, list[0].pillar)
        assertTrue(list[0].updatedAt > 0)
        val request = h.server.takeRequest()
        assertEquals("/api/v1/guidelines?page=1&pageSize=200", request.path)
        assertEquals("Bearer access-1", request.getHeader("Authorization"))
    }

    @Test fun `create sends only content fields and refreshes the list immediately`() = runBlocking {
        h.server.enqueue(MockResponse().json(page(g("1", "Antiga"))))
        h.server.enqueue(MockResponse().json(g("2", "Nova"), 201))
        h.server.enqueue(MockResponse().json(page(g("2", "Nova"), g("1", "Antiga"))))

        repo.observeAll().test {
            assertEquals(listOf("Antiga"), awaitItem().map { it.title })

            val outcome = repo.create("  Nova  ", " desc ", Pillar.PROJETOS, "ignorado", "ignorado", " Camp ")

            assertEquals(Outcome.Success("2"), outcome)
            assertEquals(listOf("Nova", "Antiga"), awaitItem().map { it.title }) // no topo, sem esperar os 15 s
            cancelAndIgnoreRemainingEvents()
        }
        h.server.takeRequest()
        val post = h.server.takeRequest()
        val body = post.body.readUtf8()
        assertEquals("POST", post.method)
        assertTrue(body.contains("\"title\":\"Nova\"") && body.contains("\"campaign\":\"Camp\"") && body.contains("\"pillar\":\"PROJETOS\""))
        assertTrue("autor não vai no corpo (o servidor usa o token)", !body.contains("ignorado") && !body.contains("authorId"))
    }

    @Test fun `update is a PUT and delete is a DELETE, both invalidating the list`() = runBlocking {
        h.server.enqueue(MockResponse().json(g("1", "X")))
        h.server.enqueue(MockResponse().setResponseCode(204))

        assertEquals(Outcome.Success(Unit), repo.update("1", "X", "d", Pillar.MENSURACAO, null))
        assertEquals(Outcome.Success(Unit), repo.delete("1"))

        val put = h.server.takeRequest(); val delete = h.server.takeRequest()
        assertEquals("PUT /api/v1/guidelines/1", "${put.method} ${put.path}")
        assertEquals("DELETE /api/v1/guidelines/1", "${delete.method} ${delete.path}")
    }

    @Test fun `forbidden write for a non leader becomes PermissionDenied and does not invalidate`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(403, "FORBIDDEN", "Você não tem permissão para esta ação."), 403))

        bus.events.test {
            val outcome = repo.create("T", "D", Pillar.IDEIAS, "", "")

            assertEquals(DomainError.PermissionDenied("Você não tem permissão para esta ação."), (outcome as Outcome.Failure).error)
            expectNoEvents()
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `observe emits null when the guideline does not exist`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(404, "RESOURCE_NOT_FOUND", "Orientação não encontrada."), 404))
        assertNull(repo.observe("nao-existe").first())
    }

    @Test fun `all pages are fetched when the server has more than one page`() = runBlocking {
        h.server.enqueue(MockResponse().json("""{"items":[${g("1", "A")}],"page":1,"pageSize":1,"totalItems":2}"""))
        h.server.enqueue(MockResponse().json("""{"items":[${g("2", "B")}],"page":2,"pageSize":1,"totalItems":2}"""))

        assertEquals(listOf("A", "B"), repo.observeAll().first().map { it.title })
        h.server.takeRequest()
        assertEquals("/api/v1/guidelines?page=2&pageSize=200", h.server.takeRequest().path)
    }
}
