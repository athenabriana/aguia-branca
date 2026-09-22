package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.network.api.AuthApi
import com.aguiabranca.app.core.network.dto.RefreshRequestDto
import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route
import retrofit2.HttpException
import java.io.IOException

/**
 * Em `401`, tenta **um** refresh (serializado: várias chamadas simultâneas com 401 geram um único refresh; as demais
 * reaproveitam o token novo) e repete a requisição. Refresh recusado pelo servidor → limpa a sessão e avisa a UI
 * (`SessionEvents`). Falha de rede no refresh NÃO derruba a sessão (pode ser só instabilidade).
 */
class TokenAuthenticator(
    private val store: TokenStore,
    private val refreshApi: () -> AuthApi,
    private val events: SessionEvents
) : Authenticator {

    private val lock = Any()

    override fun authenticate(route: Route?, response: Response): Request? {
        val request = response.request
        if (request.isPublicAuthCall()) return null          // 401 de login/refresh: credenciais erradas, não há o que renovar
        if (responseCount(response) >= 2) return null        // já retentou: não entra em laço
        val sentToken = request.header("Authorization")?.removePrefix("Bearer ")?.trim() ?: return null

        synchronized(lock) {
            val current = runBlocking { store.get() } ?: return null

            // Outra chamada já renovou enquanto esta esperava: só repete com o token novo.
            if (current.accessToken != sentToken) return request.withToken(current.accessToken)

            return try {
                val renewed = runBlocking { refreshApi().refresh(RefreshRequestDto(current.refreshToken)) }
                val tokens = Tokens(renewed.accessToken, renewed.refreshToken)
                runBlocking { store.save(tokens) }
                request.withToken(tokens.accessToken)
            } catch (e: HttpException) {
                runBlocking { store.clear() }
                events.notifyExpired()
                null
            } catch (e: IOException) {
                null
            }
        }
    }

    private fun Request.withToken(token: String): Request = newBuilder().header("Authorization", "Bearer $token").build()

    private fun responseCount(response: Response): Int {
        var count = 1
        var prior = response.priorResponse
        while (prior != null) { count++; prior = prior.priorResponse }
        return count
    }
}
