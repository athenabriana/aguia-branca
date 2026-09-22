package com.aguiabranca.app.core.auth

import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.AuthSession
import com.aguiabranca.app.core.network.SessionEvents
import com.aguiabranca.app.core.network.TokenStore
import com.aguiabranca.app.core.network.Tokens
import com.aguiabranca.app.core.network.api.AuthApi
import com.aguiabranca.app.core.network.dto.LoginRequestDto
import com.aguiabranca.app.core.network.dto.LogoutRequestDto
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Sessão via API (endpoints de autenticação). Os tokens ficam cifrados no [TokenStore]; ao reabrir o app a sessão é restaurada com
 * `/auth/me`. Um refresh recusado pelo servidor ([SessionEvents]) encerra a sessão e a UI volta ao Login.
 */
@Singleton
class SessionManager internal constructor(
    private val authApi: AuthApi,
    private val store: TokenStore,
    events: SessionEvents,
    private val scope: CoroutineScope
) {
    @Inject constructor(authApi: AuthApi, store: TokenStore, events: SessionEvents) :
        this(authApi, store, events, CoroutineScope(SupervisorJob() + Dispatchers.Default))

    private val _currentUser = MutableStateFlow<AuthSession?>(null)
    val currentUser: StateFlow<AuthSession?> = _currentUser.asStateFlow()

    /** `true` até a restauração inicial terminar (a splash espera, para não piscar a tela de Login). */
    private val _restoring = MutableStateFlow(true)
    val restoring: StateFlow<Boolean> = _restoring.asStateFlow()

    init {
        scope.launch { events.expired.collect { _currentUser.value = null } }
        scope.launch { restore() }
    }

    private suspend fun restore() {
        try {
            if (store.get() == null) return
            when (val me = safeApiCall { authApi.me() }) {
                is Outcome.Success -> _currentUser.value = AuthSession.from(me.value.toDomain())
                is Outcome.Failure ->
                    // Token inválido (o authenticator já tentou o refresh) → Login. Sem rede, a sessão local é preservada,
                    // mas sem perfil não há tela inicial: o usuário vê o Login e entra de novo quando a rede voltar.
                    if (me.error is DomainError.NotAuthenticated) store.clear()
            }
        } finally {
            _restoring.value = false
        }
    }

    suspend fun signIn(email: String, password: String): Outcome<AuthSession> =
        when (val result = safeApiCall { authApi.login(LoginRequestDto(email, password)) }) {
            is Outcome.Failure -> result
            is Outcome.Success -> {
                store.save(Tokens(result.value.accessToken, result.value.refreshToken))
                val session = AuthSession.from(result.value.user.toDomain())
                _currentUser.value = session
                Outcome.Success(session)
            }
        }

    /** Encerra a sessão. Falha de rede no `/auth/logout` não impede limpar o estado local. */
    suspend fun signOut() {
        val refresh = store.get()?.refreshToken
        if (refresh != null) safeApiCall { authApi.logout(LogoutRequestDto(refresh)) }
        store.clear()
        _currentUser.value = null
    }
}
