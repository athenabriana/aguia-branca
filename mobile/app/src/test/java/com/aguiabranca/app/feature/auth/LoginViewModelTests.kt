package com.aguiabranca.app.feature.auth

import androidx.lifecycle.SavedStateHandle
import com.aguiabranca.app.core.auth.FakeAuthApi
import com.aguiabranca.app.core.auth.SessionManager
import com.aguiabranca.app.core.auth.httpError
import com.aguiabranca.app.core.auth.problem
import com.aguiabranca.app.core.auth.profile
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.network.InMemoryTokenStore
import com.aguiabranca.app.core.network.SessionEvents
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.feature.auth.ui.LoginViewModel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class LoginViewModelTests {
    private val api = FakeAuthApi()
    private lateinit var vm: LoginViewModel

    @Before fun setUp() {
        Dispatchers.setMain(UnconfinedTestDispatcher())
        val session = SessionManager(api, InMemoryTokenStore(), SessionEvents(), CoroutineScope(SupervisorJob() + UnconfinedTestDispatcher()))
        vm = LoginViewModel(SavedStateHandle(), session)
    }

    @After fun tearDown() = Dispatchers.resetMain()

    private fun fill(email: String = "gestor@x.com", password: String = "aguiabranca123") { vm.onEmailChange(email); vm.onPasswordChange(password) }

    @Test fun `successful login emits the role so the home of that profile opens`() {
        api.onLogin = { com.aguiabranca.app.core.network.dto.AuthResponseDto("a", "r", 1800, profile("LIDER")) }
        fill(); vm.submit()
        assertEquals(UiState.Success(Role.LIDER), vm.state.value)
    }

    @Test fun `invalid credentials show the specific message`() {
        api.onLogin = { throw httpError(401, problem(401, "INVALID_CREDENTIALS", "x")) }
        fill(password = "errada123"); vm.submit()
        assertEquals(UiState.Error(DomainError.NotAuthenticated("E-mail ou senha inválidos.")), vm.state.value)
    }

    @Test fun `429 shows the too many attempts message`() {
        api.onLogin = { throw httpError(429, problem(429, "RATE_LIMITED", "Muitas tentativas."), mapOf("Retry-After" to "30")) }
        fill(); vm.submit()
        val error = (vm.state.value as UiState.Error).error
        assertTrue(error is DomainError.TooManyRequests && error.retryAfterSeconds == 30)
    }

    @Test fun `no network maps to NetworkUnavailable`() {
        api.onLogin = { throw IOException("sem rede") }
        fill(); vm.submit()
        assertTrue((vm.state.value as UiState.Error).error is DomainError.NetworkUnavailable)
    }

    @Test fun `blank or short input is rejected locally without calling the server`() {
        fill(email = "", password = "123"); vm.submit()
        assertTrue((vm.state.value as UiState.Error).error is DomainError.ValidationFailed)
        assertEquals(0, api.logins.size)
    }

    @Test fun `quick login fills the form and signs in against the backend`() {
        vm.quickLogin("operador@aguiabranca.com", "aguiabranca123")
        assertEquals("operador@aguiabranca.com", vm.form.value.email)
        assertEquals("operador@aguiabranca.com", api.logins.single().email)
        assertTrue(vm.state.value is UiState.Success<*>)
    }

    @Test fun `consumeError returns to idle`() {
        api.onLogin = { throw IOException() }
        fill(); vm.submit(); vm.consumeError()
        assertEquals(UiState.Idle, vm.state.value)
    }
}
