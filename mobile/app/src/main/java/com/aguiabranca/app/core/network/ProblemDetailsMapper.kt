package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.network.dto.ProblemDetailsDto
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.json.Json
import retrofit2.HttpException
import java.io.IOException

/** `ProblemDetails` da API (e falhas de transporte) → `DomainError` (R2-08.4). */
object ProblemDetailsMapper {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    fun map(t: Throwable): DomainError = when (t) {
        is HttpException -> fromHttp(t)
        is IOException -> DomainError.NetworkUnavailable(t)   // inclui timeout, sem rota e falha de DNS
        else -> DomainError.Unknown(t)
    }

    private fun fromHttp(e: HttpException): DomainError {
        val problem = parse(e)
        val first = problem?.errors?.firstOrNull()
        val detail = first?.message ?: problem?.detail
        val code = problem?.code

        return when (e.code()) {
            401 -> DomainError.NotAuthenticated(
                if (code == "INVALID_CREDENTIALS") "E-mail ou senha inválidos." else detail
            )
            403 -> DomainError.PermissionDenied(detail)
            404 -> DomainError.NotFound("o recurso solicitado")
            400, 422 -> DomainError.ValidationFailed(first?.field ?: "dados", detail ?: "valor inválido")
            409 -> DomainError.ConflictingState(
                if (code == "CONCURRENCY_CONFLICT") "O projeto foi alterado por outro gestor. Recarregue e tente novamente."
                else detail ?: "A operação conflita com o estado atual."
            )
            429 -> DomainError.TooManyRequests(
                detail ?: "Muitas tentativas. Aguarde um pouco e tente novamente.",
                e.response()?.headers()?.get("Retry-After")?.toIntOrNull()
            )
            502, 503 -> DomainError.ServiceUnavailable(code, detail)
            else -> DomainError.Unknown(e)
        }
    }

    private fun parse(e: HttpException): ProblemDetailsDto? = runCatching {
        e.response()?.errorBody()?.string()?.takeIf { it.isNotBlank() }?.let { json.decodeFromString<ProblemDetailsDto>(it) }
    }.getOrNull()
}

/** Executa a chamada de rede e traduz falhas em `Outcome.Failure` (o cancelamento de coroutine NUNCA é engolido). */
suspend inline fun <T> safeApiCall(crossinline block: suspend () -> T): Outcome<T> = try {
    Outcome.Success(block())
} catch (c: CancellationException) {
    throw c
} catch (t: Throwable) {
    Outcome.Failure(ProblemDetailsMapper.map(t))
}
