package com.aguiabranca.app.core.di

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import com.aguiabranca.app.BuildConfig
import com.aguiabranca.app.core.network.AndroidKeystoreCipher
import com.aguiabranca.app.core.network.AuthInterceptor
import com.aguiabranca.app.core.network.DataStoreTokenStore
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.SessionEvents
import com.aguiabranca.app.core.network.TokenAuthenticator
import com.aguiabranca.app.core.network.TokenCipher
import com.aguiabranca.app.core.network.TokenStore
import com.aguiabranca.app.core.network.api.AuthApi
import com.aguiabranca.app.core.network.api.GuidelinesApi
import com.aguiabranca.app.core.network.api.IdeasApi
import com.aguiabranca.app.core.network.api.ProjectsApi
import com.aguiabranca.app.core.network.api.ReportsApi
import com.aguiabranca.app.core.network.api.UsersApi
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

private val Context.tokenDataStore: DataStore<Preferences> by preferencesDataStore(name = "session")

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides @Singleton
    fun provideJson(): Json = Json {
        ignoreUnknownKeys = true      // o servidor pode ganhar campos sem quebrar o app
        explicitNulls = false
        encodeDefaults = true
        coerceInputValues = true
    }

    @Provides @Singleton fun provideTokenCipher(): TokenCipher = AndroidKeystoreCipher()

    @Provides @Singleton
    fun provideTokenStore(@ApplicationContext context: Context, cipher: TokenCipher): TokenStore =
        DataStoreTokenStore(context.tokenDataStore, cipher)

    @Provides @Singleton fun provideSessionEvents(): SessionEvents = SessionEvents()
    @Provides @Singleton fun provideRefreshBus(): RefreshBus = RefreshBus()

    private fun baseClient(): OkHttpClient.Builder = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .callTimeout(60, TimeUnit.SECONDS) // a geração de insights de IA pode levar dezenas de segundos

    private fun retrofit(client: OkHttpClient, json: Json): Retrofit = Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    @Provides @Singleton
    fun provideOkHttp(store: TokenStore, events: SessionEvents, json: Json): OkHttpClient {
        // O refresh usa um cliente à parte (sem interceptor/authenticator): não pode disparar 401 recursivo.
        val refreshApi by lazy { retrofit(baseClient().build(), json).create(AuthApi::class.java) }
        return baseClient()
            .addInterceptor(AuthInterceptor(store))
            .authenticator(TokenAuthenticator(store, { refreshApi }, events))
            .build()
    }

    @Provides @Singleton fun provideRetrofit(client: OkHttpClient, json: Json): Retrofit = retrofit(client, json)

    @Provides @Singleton fun provideAuthApi(r: Retrofit): AuthApi = r.create(AuthApi::class.java)
    @Provides @Singleton fun provideGuidelinesApi(r: Retrofit): GuidelinesApi = r.create(GuidelinesApi::class.java)
    @Provides @Singleton fun provideIdeasApi(r: Retrofit): IdeasApi = r.create(IdeasApi::class.java)
    @Provides @Singleton fun provideProjectsApi(r: Retrofit): ProjectsApi = r.create(ProjectsApi::class.java)
    @Provides @Singleton fun provideReportsApi(r: Retrofit): ReportsApi = r.create(ReportsApi::class.java)
    @Provides @Singleton fun provideUsersApi(r: Retrofit): UsersApi = r.create(UsersApi::class.java)
}
