package com.aguiabranca.app.core.auth

import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.network.InMemoryTokenStore
import com.aguiabranca.app.core.network.SessionEvents
import com.aguiabranca.app.core.network.Tokens
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class SessionManagerTests {
    private val api = FakeAuthApi()
    private val events = SessionEvents()

    private fun TestScope.manager(store: InMemoryTokenStore = InMemoryTokenStore()) =
        SessionManager(api, store, events, backgroundScope) to store

    @Test fun `sign in stores the tokens and exposes the profile as the current session`() = runTest(UnconfinedTestDispatcher()) {
        val (m, store) = manager()
        m.restoring.first { !it }

        val result = m.signIn("gestor@x.com", "aguiabranca123")

        assertTrue(result is Outcome.Success)
        assertEquals(Role.GESTOR, m.currentUser.value?.role)
        assertEquals("u1", m.currentUser.value?.uid)
        assertEquals(Tokens("acc", "ref"), store.get())
        assertEquals("gestor@x.com", api.logins.single().email)
    }

    @Test fun `invalid credentials fail without storing anything`() = runTest(UnconfinedTestDispatcher()) {
        api.onLogin = { throw httpError(401, problem(401, "INVALID_CREDENTIALS", "x")) }
        val (m, store) = manager()

        val result = m.signIn("a@x.com", "errada123")

        assertEquals(DomainError.NotAuthenticated("E-mail ou senha inválidos."), (result as Outcome.Failure).error)
        assertNull(m.currentUser.value)
        assertNull(store.get())
    }

    @Test fun `reopening the app restores the session from the stored tokens`() = runTest(UnconfinedTestDispatcher()) {
        api.onMe = { profile("LIDER", "u9", "Líder") }
        val (m, _) = manager(InMemoryTokenStore(Tokens("a", "r")))

        m.restoring.first { !it }

        assertEquals("u9", m.currentUser.value?.uid)
        assertEquals(Role.LIDER, m.currentUser.value?.role)
        assertEquals(1, api.meCalls)
    }

    @Test fun `an invalid stored token sends the user to login and clears the tokens`() = runTest(UnconfinedTestDispatcher()) {
        api.onMe = { throw httpError(401, problem(401, "TOKEN_INVALID", "x")) }
        val (m, store) = manager(InMemoryTokenStore(Tokens("velho", "velho")))

        m.restoring.first { !it }

        assertNull(m.currentUser.value)
        assertNull(store.get())
    }

    @Test fun `no network on startup keeps the stored session for later`() = runTest(UnconfinedTestDispatcher()) {
        api.onMe = { throw IOException("sem rede") }
        val (m, store) = manager(InMemoryTokenStore(Tokens("a", "r")))

        m.restoring.first { !it }

        assertNull(m.currentUser.value)
        assertEquals(Tokens("a", "r"), store.get())
    }

    @Test fun `without stored tokens there is no request and restoring ends`() = runTest(UnconfinedTestDispatcher()) {
        val (m, _) = manager()
        m.restoring.first { !it }
        assertEquals(0, api.meCalls)
    }

    @Test fun `sign out calls logout with the refresh token and clears the session`() = runTest(UnconfinedTestDispatcher()) {
        val (m, store) = manager()
        m.signIn("a@x.com", "12345678")

        m.signOut()

        assertEquals("ref", api.logouts.single().refreshToken)
        assertNull(m.currentUser.value)
        assertNull(store.get())
    }

    @Test fun `a network failure on logout does not prevent clearing the local session`() = runTest(UnconfinedTestDispatcher()) {
        api.onLogout = { throw IOException("sem rede") }
        val (m, store) = manager()
        m.signIn("a@x.com", "12345678")

        m.signOut()

        assertNull(m.currentUser.value)
        assertNull(store.get())
    }

    @Test fun `a refresh rejected by the server ends the session`() = runTest(UnconfinedTestDispatcher()) {
        val (m, _) = manager()
        m.signIn("a@x.com", "12345678")
        assertTrue(m.currentUser.value != null)

        events.notifyExpired()

        assertNull(m.currentUser.value)
    }
}
