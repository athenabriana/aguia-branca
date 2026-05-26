package com.aguiabranca.app.feature.auth.ui

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.FirebaseNetworkException
import com.google.firebase.auth.FirebaseAuthInvalidCredentialsException
import com.google.firebase.auth.FirebaseAuthInvalidUserException
import com.aguiabranca.app.core.auth.SessionManager
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.state.UiState
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import javax.inject.Inject

data class LoginFormState(
    val email: String = "",
    val password: String = ""
)

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val savedStateHandle: SavedStateHandle,
    private val sessionManager: SessionManager
) : ViewModel() {

    private val _form = MutableStateFlow(
        LoginFormState(
            email = savedStateHandle.get<String>(KEY_EMAIL) ?: "",
            password = savedStateHandle.get<String>(KEY_PASSWORD) ?: ""
        )
    )
    val form: StateFlow<LoginFormState> = _form.asStateFlow()

    private val _state = MutableStateFlow<UiState<Role>>(UiState.Idle)
    val state: StateFlow<UiState<Role>> = _state.asStateFlow()

    fun onEmailChange(v: String) {
        _form.value = _form.value.copy(email = v)
        savedStateHandle[KEY_EMAIL] = v
    }

    fun onPasswordChange(v: String) {
        _form.value = _form.value.copy(password = v)
        savedStateHandle[KEY_PASSWORD] = v
    }

    fun submit() {
        val email = _form.value.email.trim()
        val password = _form.value.password
        if (email.isBlank() || password.length < 6) {
            _state.value = UiState.Error(DomainError.ValidationFailed("email/senha", "preencha corretamente"))
            return
        }
        _state.value = UiState.Loading
        viewModelScope.launch {
            try {
                sessionManager.signIn(email, password)
                val session = withTimeoutOrNull(10_000) {
                    sessionManager.currentUser.first { it != null }
                }
                if (session == null) {
                    _state.value = UiState.Error(DomainError.NotFound("perfil", email))
                } else {
                    _state.value = UiState.Success(session.role)
                }
            } catch (e: FirebaseAuthInvalidCredentialsException) {
                _state.value = UiState.Error(DomainError.ValidationFailed("credenciais", "inválidas"))
            } catch (e: FirebaseAuthInvalidUserException) {
                _state.value = UiState.Error(DomainError.NotFound("usuário", email))
            } catch (e: FirebaseNetworkException) {
                _state.value = UiState.Error(DomainError.NetworkUnavailable(e))
            } catch (e: Throwable) {
                _state.value = UiState.Error(DomainError.Unknown(e))
            }
        }
    }

    fun consumeError() {
        if (_state.value is UiState.Error) _state.value = UiState.Idle
    }

    private companion object {
        const val KEY_EMAIL = "login.email"
        const val KEY_PASSWORD = "login.password"
    }
}
