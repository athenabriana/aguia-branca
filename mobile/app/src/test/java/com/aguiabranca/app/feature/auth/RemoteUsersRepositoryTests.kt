package com.aguiabranca.app.feature.auth

import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.api.AuthApi
import com.aguiabranca.app.core.network.api.UsersApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.feature.auth.data.RemoteUsersRepository
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RemoteUsersRepositoryTests {
    private val h = NetworkHarness()
    private val repo = RemoteUsersRepository(h.api(AuthApi::class.java), h.api(UsersApi::class.java), RefreshBus())

    @After fun tearDown() = h.shutdown()

    @Test fun `ranking shows the points of the current month, not the total`() = runBlocking {
        h.server.enqueue(MockResponse().json("""[{"id":"o1","name":"Ana","monthPoints":265},{"id":"o2","name":"Bruno","monthPoints":15}]"""))

        val top = (repo.topByPointsThisMonth(5) as Outcome.Success).value

        assertEquals(listOf("Ana" to 265, "Bruno" to 15), top.map { it.name to it.points })
        assertTrue(top.all { it.role == Role.OPERADOR })
        assertEquals("/api/v1/users/ranking?limit=5", h.server.takeRequest().path)
    }

    @Test fun `profile comes from auth me with the badges persisted by the server`() = runBlocking {
        h.server.enqueue(MockResponse().json("""{"id":"u1","name":"Ana","email":"a@x.com","role":"OPERADOR","division":"LOGISTICA","points":265,"badges":["Primeira Ideia","Impacto Real"]}"""))

        val user = repo.observe("u1").first()!!

        assertEquals(265, user.points)
        assertEquals(listOf("Primeira Ideia", "Impacto Real"), user.badges)
        assertEquals("/api/v1/auth/me", h.server.takeRequest().path)
    }

    @Test fun `the server only exposes the own profile, so another uid yields null`() = runBlocking {
        h.server.enqueue(MockResponse().json("""{"id":"u1","name":"Ana","email":"a@x.com","role":"OPERADOR","division":"LOGISTICA","points":0,"badges":[]}"""))
        assertNull(repo.observe("outro-usuario").first())
    }

    @Test fun `managers list for the project responsible picker is sorted and filtered by role`() = runBlocking {
        h.server.enqueue(MockResponse().json("""[{"id":"m2","name":"zeca","role":"GESTOR","division":"LOGISTICA"},{"id":"m1","name":"Ana Gestora","role":"GESTOR","division":"COMERCIO"}]"""))

        val managers = (repo.listByRole(Role.GESTOR) as Outcome.Success).value

        assertEquals(listOf("Ana Gestora", "zeca"), managers.map { it.name })
        assertEquals("/api/v1/users?role=GESTOR", h.server.takeRequest().path)
    }

    @Test fun `ranking failure is an outcome, and ensureProfileExists is a no-op because accounts live on the server`() = runBlocking {
        h.server.enqueue(MockResponse().setResponseCode(503).json("{}", 503))

        assertTrue((repo.topByPointsThisMonth(5) as Outcome.Failure).error is DomainError.ServiceUnavailable)
        assertEquals(Outcome.Success(Unit), repo.ensureProfileExists("u", "e", "n", Role.OPERADOR, com.aguiabranca.app.core.domain.model.Division.LOGISTICA))
        assertEquals(1, h.server.requestCount)
    }
}
