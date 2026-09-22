package com.aguiabranca.app.feature.auth.ui

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.auth.SessionManager
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.state.UiState
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
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
            _state.value = when (val result = sessionManager.signIn(email, password)) {
                is Outcome.Success -> UiState.Success(result.value.role)
                is Outcome.Failure -> UiState.Error(result.error)
            }
        }
    }

    fun consumeError() {
        if (_state.value is UiState.Error) _state.value = UiState.Idle
    }

    fun quickLogin(email: String, password: String) {
        onEmailChange(email)
        onPasswordChange(password)
        submit()
    }

    private companion object {
        const val KEY_EMAIL = "login.email"
        const val KEY_PASSWORD = "login.password"
    }
}
