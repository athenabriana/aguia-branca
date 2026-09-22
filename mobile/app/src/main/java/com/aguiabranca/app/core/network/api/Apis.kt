package com.aguiabranca.app.core.network.api

import com.aguiabranca.app.core.network.dto.*
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

interface AuthApi {
    @POST("auth/login") suspend fun login(@Body body: LoginRequestDto): AuthResponseDto
    @POST("auth/refresh") suspend fun refresh(@Body body: RefreshRequestDto): AuthResponseDto
    @POST("auth/logout") suspend fun logout(@Body body: LogoutRequestDto)
    @GET("auth/me") suspend fun me(): UserProfileDto
}

interface GuidelinesApi {
    @GET("guidelines") suspend fun list(@Query("page") page: Int = 1, @Query("pageSize") pageSize: Int = PAGE_SIZE): PagedDto<GuidelineDto>
    @GET("guidelines/{id}") suspend fun get(@Path("id") id: String): GuidelineDto
    @POST("guidelines") suspend fun create(@Body body: GuidelineRequestDto): GuidelineDto
    @PUT("guidelines/{id}") suspend fun update(@Path("id") id: String, @Body body: GuidelineRequestDto): GuidelineDto
    @DELETE("guidelines/{id}") suspend fun delete(@Path("id") id: String)
}

interface IdeasApi {
    @GET("ideas")
    suspend fun list(
        @Query("scope") scope: String? = null,
        @Query("status") status: String? = null,
        @Query("guidelineId") guidelineId: String? = null,
        @Query("division") division: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = PAGE_SIZE
    ): PagedDto<IdeaDto>

    @GET("ideas/{id}") suspend fun get(@Path("id") id: String): IdeaDto
    @POST("ideas") suspend fun create(@Body body: IdeaRequestDto): IdeaDto
    @PUT("ideas/{id}") suspend fun update(@Path("id") id: String, @Body body: IdeaRequestDto): IdeaDto
    @DELETE("ideas/{id}") suspend fun delete(@Path("id") id: String)
    @PUT("ideas/{id}/ice") suspend fun saveIce(@Path("id") id: String, @Body body: IceRequestDto): IdeaDto
    @POST("ideas/{id}/reject") suspend fun reject(@Path("id") id: String, @Body body: RejectRequestDto): IdeaDto
    @POST("ideas/{id}/approve") suspend fun approve(@Path("id") id: String): ApproveResultDto
}

interface ProjectsApi {
    @GET("projects")
    suspend fun list(
        @Query("stage") stage: String? = null,
        @Query("division") division: String? = null,
        @Query("guidelineId") guidelineId: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = PAGE_SIZE
    ): PagedDto<ProjectDto>

    @GET("projects/{id}") suspend fun get(@Path("id") id: String): ProjectDto
    @GET("projects/{id}/updates")
    suspend fun updates(@Path("id") id: String, @Query("page") page: Int = 1, @Query("pageSize") pageSize: Int = PAGE_SIZE): PagedDto<ProjectUpdateDto>
    @POST("projects") suspend fun create(@Body body: ProjectRequestDto): ProjectDto
    @PUT("projects/{id}") suspend fun update(@Path("id") id: String, @Body body: ProjectRequestDto): ProjectDto
    @DELETE("projects/{id}") suspend fun delete(@Path("id") id: String)
}

interface ReportsApi {
    @GET("reports/summary") suspend fun summary(@Query("period") period: String, @Query("division") division: String? = null): ReportSummaryDto
    @POST("reports/insights") suspend fun insights(@Body body: InsightsRequestDto): InsightDto
}

interface UsersApi {
    @GET("users") suspend fun list(@Query("role") role: String? = null): List<UserSummaryDto>
    @GET("users/ranking") suspend fun ranking(@Query("limit") limit: Int = 5): List<RankingEntryDto>
}

/** Máximo aceito pela API (`pageSize` ≤ 200): as listas do app são pequenas e cabem numa página. */
const val PAGE_SIZE = 200
