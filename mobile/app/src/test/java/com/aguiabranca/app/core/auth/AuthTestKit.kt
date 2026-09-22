package com.aguiabranca.app.core.auth

import com.aguiabranca.app.core.network.dto.AuthResponseDto
import com.aguiabranca.app.core.network.dto.LoginRequestDto
import com.aguiabranca.app.core.network.dto.LogoutRequestDto
import com.aguiabranca.app.core.network.dto.RefreshRequestDto
import com.aguiabranca.app.core.network.dto.UserProfileDto
import com.aguiabranca.app.core.network.api.AuthApi
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.HttpException
import retrofit2.Response
import java.io.IOException

fun profile(role: String = "GESTOR", id: String = "u1", name: String = "Gestor") =
    UserProfileDto(id = id, name = name, email = "$id@x.com", role = role, division = "LOGISTICA", points = 10, badges = listOf("Primeira Ideia"))

fun httpError(code: Int, body: String = "{}", headers: Map<String, String> = emptyMap()): HttpException {
    val raw = okhttp3.Response.Builder().code(code).message("x").protocol(okhttp3.Protocol.HTTP_1_1)
        .request(okhttp3.Request.Builder().url("http://x/").build()).apply { headers.forEach { (k, v) -> header(k, v) } }.build()
    return HttpException(Response.error<Any>(body.toResponseBody("application/problem+json".toMediaType()), raw))
}

fun problem(status: Int, code: String, detail: String) =
    """{"status":$status,"code":"$code","detail":"$detail","errors":[{"code":"$code","message":"$detail"}]}"""

/** AuthApi de mentira: cada chamada devolve/lança o que o teste configurou. */
class FakeAuthApi : AuthApi {
    var onLogin: suspend (LoginRequestDto) -> AuthResponseDto = { AuthResponseDto("acc", "ref", 1800, profile()) }
    var onMe: suspend () -> UserProfileDto = { profile() }
    var onLogout: suspend (LogoutRequestDto) -> Unit = {}
    val logins = mutableListOf<LoginRequestDto>()
    val logouts = mutableListOf<LogoutRequestDto>()
    var meCalls = 0

    override suspend fun login(body: LoginRequestDto): AuthResponseDto { logins += body; return onLogin(body) }
    override suspend fun refresh(body: RefreshRequestDto): AuthResponseDto = error("não usado")
    override suspend fun logout(body: LogoutRequestDto) { logouts += body; onLogout(body) }
    override suspend fun me(): UserProfileDto { meCalls++; return onMe() }
}
