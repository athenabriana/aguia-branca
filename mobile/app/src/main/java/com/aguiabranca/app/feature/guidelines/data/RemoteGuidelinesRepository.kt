package com.aguiabranca.app.feature.guidelines.data

import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.GuidelinesApi
import com.aguiabranca.app.core.network.dto.GuidelineRequestDto
import com.aguiabranca.app.core.network.fetchAll
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.pollingFlow
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.flow.Flow
import retrofit2.HttpException
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Orientações via REST. A "lista ao vivo" é um polling de 15 s com invalidação imediata após cada escrita, então quem
 * cria/edita/exclui vê o resultado na hora. Autor e datas são definidos pelo servidor (o token identifica o líder).
 */
@Singleton
class RemoteGuidelinesRepository @Inject constructor(
    private val api: GuidelinesApi,
    private val bus: RefreshBus
) : GuidelinesRepository {

    private val keys = setOf(RefreshKeys.GUIDELINES)

    override fun observeAll(): Flow<List<Guideline>> = pollingFlow(bus, keys) {
        fetchAll { page -> api.list(page) }.map { it.toDomain() }
    }

    override fun observe(id: String): Flow<Guideline?> = pollingFlow(bus, keys) {
        try { api.get(id).toDomain() } catch (e: HttpException) { if (e.code() == 404) null else throw e }
    }

    override suspend fun create(
        title: String, description: String, pillar: Pillar, authorId: String, authorName: String, campaign: String?
    ): Outcome<String> = safeApiCall {
        api.create(GuidelineRequestDto(title.trim(), description.trim(), pillar.name, campaign?.trim()?.ifEmpty { null })).id
    }.also { invalidateOnSuccess(it) }

    override suspend fun update(id: String, title: String, description: String, pillar: Pillar, campaign: String?): Outcome<Unit> =
        safeApiCall {
            api.update(id, GuidelineRequestDto(title.trim(), description.trim(), pillar.name, campaign?.trim()?.ifEmpty { null }))
            Unit
        }.also { invalidateOnSuccess(it) }

    override suspend fun delete(id: String): Outcome<Unit> = safeApiCall { api.delete(id) }.also { invalidateOnSuccess(it) }

    private fun invalidateOnSuccess(outcome: Outcome<*>) {
        // A exclusão de uma orientação também muda os relatórios (ela some do impacto por orientação).
        if (outcome is Outcome.Success) bus.invalidate(RefreshKeys.GUIDELINES, RefreshKeys.REPORTS)
    }
}
