package com.aguiabranca.app.feature.ideas.data

import com.aguiabranca.app.core.domain.CreateIdeaInput
import com.aguiabranca.app.core.domain.CreatedIdea
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.UpdateIdeaInput
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.IdeasApi
import com.aguiabranca.app.core.network.dto.IceRequestDto
import com.aguiabranca.app.core.network.dto.IdeaRequestDto
import com.aguiabranca.app.core.network.dto.RejectRequestDto
import com.aguiabranca.app.core.network.fetchAll
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.pollingFlow
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.flow.Flow
import retrofit2.HttpException
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Ideias via REST. Regras (pontos, ICE, aprovação, visibilidade) são do servidor: o operador só recebe as próprias ideias
 * e nenhum crédito é calculado aqui. Escritas invalidam as listas (e o perfil/ranking quando mexem em pontos).
 */
@Singleton
class RemoteIdeasRepository @Inject constructor(
    private val api: IdeasApi,
    private val bus: RefreshBus
) : IdeasRepository {

    private val keys = setOf(RefreshKeys.IDEAS)

    private fun list(scope: String, guidelineId: String? = null): Flow<List<Idea>> = pollingFlow(bus, keys) {
        fetchAll { page -> api.list(scope = scope, guidelineId = guidelineId, page = page) }.map { it.toDomain() }
    }

    override fun observeAll(): Flow<List<Idea>> = list("all")

    /** `authorId` é ignorado de propósito: `scope=mine` usa a identidade do token. */
    override fun observeByAuthor(authorId: String): Flow<List<Idea>> = list("mine")

    override fun observeForCuration(): Flow<List<Idea>> = list("curation")

    override fun observeByGuideline(guidelineId: String): Flow<List<Idea>> = list("all", guidelineId)

    override fun observe(id: String): Flow<Idea?> = pollingFlow(bus, keys) {
        try { api.get(id).toDomain() } catch (e: HttpException) { if (e.code() == 404) null else throw e }
    }

    override suspend fun createIdea(input: CreateIdeaInput): Outcome<CreatedIdea> = safeApiCall {
        val created = api.create(
            IdeaRequestDto(input.title.trim(), input.description.trim(), input.category.trim(), input.division.name, input.guidelineId)
        )
        CreatedIdea(created.id, created.pointsAwarded ?: 0)
    }.also { changed(it, points = true) }

    override suspend fun updateIdea(id: String, input: UpdateIdeaInput): Outcome<Unit> = safeApiCall {
        api.update(id, IdeaRequestDto(input.title.trim(), input.description.trim(), input.category.trim(), input.division.name, input.guidelineId))
        Unit
    }.also { changed(it) }

    override suspend fun deleteIdea(id: String, authorId: String): Outcome<Unit> =
        safeApiCall { api.delete(id) }.also { changed(it, points = true) }

    override suspend fun saveIce(id: String, ice: Ice, reviewerId: String): Outcome<Unit> {
        if (!ice.isComplete) return Outcome.Failure(DomainError.ValidationFailed("ice", "informe impacto, confiança e facilidade de 1 a 10"))
        return safeApiCall { api.saveIce(id, IceRequestDto(ice.impact, ice.confidence, ice.ease)); Unit }.also { changed(it) }
    }

    override suspend fun rejectIdea(id: String, reviewerId: String, comment: String): Outcome<Unit> {
        if (comment.isBlank()) return Outcome.Failure(DomainError.ValidationFailed("reviewComment", "obrigatório"))
        return safeApiCall { api.reject(id, RejectRequestDto(comment.trim())); Unit }.also { changed(it, reports = true) }
    }

    private fun changed(outcome: Outcome<*>, points: Boolean = false, reports: Boolean = false) {
        if (outcome !is Outcome.Success) return
        val touched = mutableListOf(RefreshKeys.IDEAS)
        if (points) touched += listOf(RefreshKeys.ME, RefreshKeys.RANKING)
        if (points || reports) touched += RefreshKeys.REPORTS
        bus.invalidate(*touched.toTypedArray())
    }
}
