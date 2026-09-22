package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.network.api.AuthApi
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import retrofit2.http.GET
import java.util.concurrent.TimeUnit

class InMemoryTokenStore(initial: Tokens? = null) : TokenStore {
    @Volatile var tokens: Tokens? = initial
    @Volatile var saves = 0
    override suspend fun get(): Tokens? = tokens
    override suspend fun save(tokens: Tokens) { this.tokens = tokens; saves++ }
    override suspend fun clear() { tokens = null }
}

/** Cifra "de mentira" (só para provar que o disco nunca recebe o valor original). */
class ReversingCipher : TokenCipher {
    override fun encrypt(plain: String) = "enc:" + plain.reversed()
    override fun decrypt(encrypted: String): String? = encrypted.removePrefix("enc:").reversed().takeIf { encrypted.startsWith("enc:") }
}

interface PingApi {
    @GET("ping") suspend fun ping(): PingDto
    @GET("boom") suspend fun boom(): PingDto
}

@kotlinx.serialization.Serializable data class PingDto(val ok: Boolean = true)

val TestJson = Json { ignoreUnknownKeys = true; explicitNulls = false; encodeDefaults = true; coerceInputValues = true }

fun authJson(access: String, refresh: String) =
    """{"accessToken":"$access","refreshToken":"$refresh","expiresIn":1800,"user":{"id":"u1","name":"Ana","email":"a@x.com","role":"OPERADOR","division":"LOGISTICA","points":0,"badges":[]}}"""

fun problemJson(status: Int, code: String, detail: String, field: String? = null, message: String? = null) =
    """{"type":"urn:x","title":"t","status":$status,"detail":"$detail","code":"$code","errors":[{"code":"$code","message":"${message ?: detail}","field":${if (field == null) "null" else "\"$field\""}}]}"""

fun MockResponse.json(body: String, code: Int = 200): MockResponse =
    setResponseCode(code).setHeader("Content-Type", "application/json").setBody(body)

/** Cliente completo (interceptor + authenticator) contra um MockWebServer. */
class NetworkHarness(store: TokenStore = InMemoryTokenStore(Tokens("access-1", "refresh-1"))) {
    val server = MockWebServer().apply { start() }
    val store: TokenStore = store
    val events = SessionEvents()
    var expiredCount = 0

    private fun retrofit(client: OkHttpClient) = Retrofit.Builder()
        .baseUrl(server.url("/api/v1/"))
        .client(client)
        .addConverterFactory(TestJson.asConverterFactory("application/json".toMediaType()))
        .build()

    private val plain = OkHttpClient.Builder().callTimeout(5, TimeUnit.SECONDS).build()
    val authApi: AuthApi = retrofit(plain).create(AuthApi::class.java)

    val client: OkHttpClient = OkHttpClient.Builder()
        .callTimeout(10, TimeUnit.SECONDS)
        .addInterceptor(AuthInterceptor(store))
        .authenticator(TokenAuthenticator(store, { authApi }, events))
        .build()

    val ping: PingApi = retrofit(client).create(PingApi::class.java)
    fun <T> api(type: Class<T>): T = retrofit(client).create(type)
    fun shutdown() = server.shutdown()
}
