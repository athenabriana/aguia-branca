package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.network.dto.LoginRequestDto
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.RecordedRequest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.HttpException

class AuthenticationTests {
    private val h = NetworkHarness()

    @After fun tearDown() = h.shutdown()

    @Test fun `injects Bearer on protected calls but never on login or refresh`() = runBlocking {
        h.server.enqueue(MockResponse().json("""{"ok":true}"""))
        h.ping.ping()
        assertEquals("Bearer access-1", h.server.takeRequest().getHeader("Authorization"))

        h.server.enqueue(MockResponse().json(authJson("a", "r")))
        h.authApi.login(LoginRequestDto("a@x.com", "12345678"))
        assertNull(h.server.takeRequest().getHeader("Authorization"))

        h.server.enqueue(MockResponse().json(authJson("a", "r")))
        h.client.newCall(okhttp3.Request.Builder().url(h.server.url("/api/v1/auth/refresh")).post(okhttp3.RequestBody.create(null, "{}")).build()).execute().close()
        assertNull(h.server.takeRequest().getHeader("Authorization"))
    }

    @Test fun `without a stored session the request goes out without a token`() = runBlocking {
        val bare = NetworkHarness(InMemoryTokenStore(null))
        try {
            bare.server.enqueue(MockResponse().json("""{"ok":true}"""))
            bare.ping.ping()
            assertNull(bare.server.takeRequest().getHeader("Authorization"))
        } finally { bare.shutdown() }
    }

    @Test fun `401 triggers one transparent refresh and repeats the request with the new token`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "expirado"), 401))
        h.server.enqueue(MockResponse().json(authJson("access-2", "refresh-2")))
        h.server.enqueue(MockResponse().json("""{"ok":true}"""))

        val result = h.ping.ping()

        assertTrue(result.ok)
        val first = h.server.takeRequest(); val refresh = h.server.takeRequest(); val retry = h.server.takeRequest()
        assertEquals("Bearer access-1", first.getHeader("Authorization"))
        assertTrue(refresh.path!!.endsWith("/auth/refresh"))
        assertTrue(refresh.body.readUtf8().contains("refresh-1"))
        assertNull(refresh.getHeader("Authorization"))
        assertEquals("Bearer access-2", retry.getHeader("Authorization"))
        assertEquals(Tokens("access-2", "refresh-2"), h.store.get())
    }

    @Test fun `simultaneous 401s produce a single refresh`() = runBlocking {
        h.server.dispatcher = object : Dispatcher() {
            @Volatile var refreshed = false
            override fun dispatch(request: RecordedRequest): MockResponse = when {
                request.path!!.endsWith("/auth/refresh") -> { refreshed = true; Thread.sleep(150); MockResponse().json(authJson("access-2", "refresh-2")) }
                request.getHeader("Authorization") == "Bearer access-2" -> MockResponse().json("""{"ok":true}""")
                else -> MockResponse().json(problemJson(401, "TOKEN_INVALID", "expirado"), 401)
            }
        }

        val results = (1..4).map { async(kotlinx.coroutines.Dispatchers.IO) { h.ping.ping() } }.awaitAll()

        assertTrue(results.all { it.ok })
        assertEquals("apenas um refresh", 1, generateSequence { h.server.takeRequestOrNull() }.count { it.path!!.endsWith("/auth/refresh") })
        assertEquals(1, (h.store as InMemoryTokenStore).saves)
    }

    @Test fun `refresh rejected by the server clears the session and signals logout`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "expirado"), 401))
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "refresh inválido"), 401))

        val expired = async { withTimeout(5_000) { h.events.expired.first() } }
        kotlinx.coroutines.yield()
        val error = runCatching { h.ping.ping() }.exceptionOrNull()

        assertTrue(error is HttpException && error.code() == 401)
        assertNull("sessão limpa", h.store.get())
        expired.await() // sinal de logout emitido
        assertEquals("sem laço: 1 chamada + 1 refresh", 2, h.server.requestCount)
    }

    @Test fun `network failure during refresh keeps the session`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "expirado"), 401))
        h.server.enqueue(MockResponse().setSocketPolicy(okhttp3.mockwebserver.SocketPolicy.DISCONNECT_AT_START))

        runCatching { h.ping.ping() }

        assertEquals(Tokens("access-1", "refresh-1"), h.store.get())
    }

    @Test fun `does not loop when the new token is also rejected`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "x"), 401))
        h.server.enqueue(MockResponse().json(authJson("access-2", "refresh-2")))
        h.server.enqueue(MockResponse().json(problemJson(401, "TOKEN_INVALID", "x"), 401))

        val error = runCatching { h.ping.ping() }.exceptionOrNull()

        assertTrue(error is HttpException && error.code() == 401)
        assertEquals("chamada, refresh, retentativa — e para", 3, h.server.requestCount)
    }

    @Test fun `401 on login is a plain credentials error without refresh`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(401, "INVALID_CREDENTIALS", "E-mail ou senha inválidos."), 401))

        val error = runCatching { h.authApi.login(LoginRequestDto("a@x.com", "errada123")) }.exceptionOrNull()

        assertTrue(error is HttpException)
        assertEquals(1, h.server.requestCount)
    }

    private fun okhttp3.mockwebserver.MockWebServer.takeRequestOrNull(): RecordedRequest? =
        runCatching { takeRequest(200, java.util.concurrent.TimeUnit.MILLISECONDS) }.getOrNull()
}
