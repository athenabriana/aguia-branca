package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.runBlocking
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.HttpException
import retrofit2.Response
import java.io.IOException
import java.net.SocketTimeoutException
import java.net.UnknownHostException

class ProblemDetailsMapperTests {
    private fun http(code: Int, body: String, headers: Map<String, String> = emptyMap()): HttpException {
        val raw = okhttp3.Response.Builder().code(code).message("x").protocol(okhttp3.Protocol.HTTP_1_1)
            .request(okhttp3.Request.Builder().url("http://x/").build()).apply { headers.forEach { (k, v) -> header(k, v) } }.build()
        return HttpException(Response.error<Any>(body.toResponseBody("application/problem+json".toMediaType()), raw))
    }

    @Test fun `401 maps to NotAuthenticated with a specific message for invalid credentials`() {
        val plain = ProblemDetailsMapper.map(http(401, problemJson(401, "TOKEN_INVALID", "Token inválido.")))
        val creds = ProblemDetailsMapper.map(http(401, problemJson(401, "INVALID_CREDENTIALS", "x")))
        assertEquals(DomainError.NotAuthenticated("Token inválido."), plain)
        assertEquals(DomainError.NotAuthenticated("E-mail ou senha inválidos."), creds)
    }

    @Test fun `403 maps to PermissionDenied keeping the server message`() {
        val e = ProblemDetailsMapper.map(http(403, problemJson(403, "SELF_APPROVAL_FORBIDDEN", "Você não pode aprovar a própria ideia.")))
        assertEquals(DomainError.PermissionDenied("Você não pode aprovar a própria ideia."), e)
    }

    @Test fun `404 maps to NotFound`() {
        assertTrue(ProblemDetailsMapper.map(http(404, problemJson(404, "RESOURCE_NOT_FOUND", "nada"))) is DomainError.NotFound)
    }

    @Test fun `400 and 422 map to ValidationFailed preferring errors array field and message`() {
        val bad = ProblemDetailsMapper.map(http(400, problemJson(400, "VALIDATION_ERROR", "geral", field = "title", message = "Título é obrigatório.")))
        val unprocessable = ProblemDetailsMapper.map(http(422, problemJson(422, "GUIDELINE_NOT_FOUND", "A orientação não existe.", field = "guidelineId")))
        assertEquals(DomainError.ValidationFailed("title", "Título é obrigatório."), bad)
        assertEquals(DomainError.ValidationFailed("guidelineId", "A orientação não existe."), unprocessable)
    }

    @Test fun `409 maps to ConflictingState with the concurrency wording for projects`() {
        val concurrency = ProblemDetailsMapper.map(http(409, problemJson(409, "CONCURRENCY_CONFLICT", "x"))) as DomainError.ConflictingState
        val other = ProblemDetailsMapper.map(http(409, problemJson(409, "IDEA_NOT_EDITABLE", "Não editável."))) as DomainError.ConflictingState
        assertTrue(concurrency.message.contains("outro gestor"))
        assertEquals("Não editável.", other.message)
    }

    @Test fun `429 maps to TooManyRequests with Retry-After`() {
        val e = ProblemDetailsMapper.map(http(429, problemJson(429, "RATE_LIMITED", "Muitas requisições."), mapOf("Retry-After" to "42")))
        assertEquals(DomainError.TooManyRequests("Muitas requisições.", 42), e)
    }

    @Test fun `502 and 503 map to ServiceUnavailable with the AI code`() {
        assertEquals(DomainError.ServiceUnavailable("AI_UNAVAILABLE", "IA fora."), ProblemDetailsMapper.map(http(503, problemJson(503, "AI_UNAVAILABLE", "IA fora."))))
        assertEquals("AI_INVALID_RESPONSE", (ProblemDetailsMapper.map(http(502, problemJson(502, "AI_INVALID_RESPONSE", "x"))) as DomainError.ServiceUnavailable).code)
    }

    @Test fun `transport failures map to NetworkUnavailable`() {
        listOf(IOException("x"), SocketTimeoutException("t"), UnknownHostException("h")).forEach {
            assertTrue(ProblemDetailsMapper.map(it) is DomainError.NetworkUnavailable)
        }
    }

    @Test fun `body that is not ProblemDetails still yields a sensible error`() {
        assertTrue(ProblemDetailsMapper.map(http(400, "<html>oops</html>")) is DomainError.ValidationFailed)
        assertTrue(ProblemDetailsMapper.map(http(500, "")) is DomainError.Unknown)
        assertTrue(ProblemDetailsMapper.map(IllegalStateException("x")) is DomainError.Unknown)
    }

    @Test fun `safeApiCall wraps success and failures but never swallows cancellation`() = runBlocking {
        assertEquals(Outcome.Success(3), safeApiCall { 3 })
        assertTrue((safeApiCall<Int> { throw IOException() } as Outcome.Failure).error is DomainError.NetworkUnavailable)
        val cancelled = runCatching { safeApiCall<Int> { throw CancellationException("cancelado") } }.exceptionOrNull()
        assertTrue(cancelled is CancellationException)
    }
}
