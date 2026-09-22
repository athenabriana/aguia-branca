package com.aguiabranca.app.core.network

import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response

/** Rotas de autenticação que NÃO levam Bearer (login e refresh usam credenciais no corpo). */
internal fun okhttp3.Request.isPublicAuthCall(): Boolean {
    val path = url.encodedPath
    return path.endsWith("/auth/login") || path.endsWith("/auth/refresh")
}

class AuthInterceptor(private val store: TokenStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.isPublicAuthCall() || request.header("Authorization") != null) return chain.proceed(request)

        val token = runBlocking { store.get() }?.accessToken
            ?: return chain.proceed(request) // sem sessão: segue sem token (o servidor responde 401)
        return chain.proceed(request.newBuilder().header("Authorization", "Bearer $token").build())
    }
}
