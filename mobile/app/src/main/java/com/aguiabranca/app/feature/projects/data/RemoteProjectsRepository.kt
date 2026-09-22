package com.aguiabranca.app.feature.projects.data

import com.aguiabranca.app.core.domain.ProjectInput
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.ProjectsApi
import com.aguiabranca.app.core.network.dto.ProjectRequestDto
import com.aguiabranca.app.core.network.fetchAll
import com.aguiabranca.app.core.network.mapper.Iso
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.pollingFlow
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.flow.Flow
import retrofit2.HttpException
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Projetos via REST (leitura: gestor e líder; escrita: só gestor — o servidor responde 403 caso contrário).
 * `PUT` é substituição completa; mudar o estágio para CONCLUIDO conclui a ideia de origem **no servidor** (o app não faz
 * escritas extras). Cada edição gera uma entrada no histórico, com o diff calculado pelo servidor.
 */
@Singleton
class RemoteProjectsRepository @Inject constructor(
    private val api: ProjectsApi,
    private val bus: RefreshBus
) : ProjectsRepository {

    private val keys = setOf(RefreshKeys.PROJECTS)

    override fun observeAll(): Flow<List<Project>> = pollingFlow(bus, keys) {
        fetchAll { page -> api.list(page = page) }.map { it.toDomain() }
    }

    override fun observe(id: String): Flow<Project?> = pollingFlow(bus, keys) {
        try { api.get(id).toDomain() } catch (e: HttpException) { if (e.code() == 404) null else throw e }
    }

    override fun observeUpdates(projectId: String): Flow<List<ProjectUpdate>> = pollingFlow(bus, keys) {
        fetchAll { page -> api.updates(projectId, page) }.map { it.toDomain() }
    }

    override fun observeByGuideline(guidelineId: String): Flow<List<Project>> = pollingFlow(bus, keys) {
        fetchAll { page -> api.list(guidelineId = guidelineId, page = page) }.map { it.toDomain() }
    }

    /** Criador e origem são definidos pelo servidor (token / aprovação da ideia): os parâmetros são ignorados de propósito. */
    override suspend fun create(
        input: ProjectInput, creatorManagerId: String, creatorManagerName: String, originatingIdeaId: String?
    ): Outcome<String> = safeApiCall { api.create(input.toRequest(note = null)).id }.also { changed(it) }

    override suspend fun update(
        id: String, input: ProjectInput, editorManagerId: String, editorManagerName: String, note: String
    ): Outcome<Unit> = safeApiCall { api.update(id, input.toRequest(note.trim().ifEmpty { null })); Unit }.also { changed(it) }

    override suspend fun delete(id: String): Outcome<Unit> = safeApiCall { api.delete(id) }.also { changed(it) }

    /** Editar/concluir muda o dashboard e pode implementar a ideia de origem (e dar pontos ao autor). */
    private fun changed(outcome: Outcome<*>) {
        if (outcome is Outcome.Success) bus.invalidate(RefreshKeys.PROJECTS, RefreshKeys.REPORTS, RefreshKeys.IDEAS, RefreshKeys.RANKING)
    }

    private fun ProjectInput.toRequest(note: String?) = ProjectRequestDto(
        title = title.trim(), description = description.trim(), stage = stage.name, statusText = statusText.trim(),
        investment = investment, targetDate = targetDate?.let(Iso::format), financialReturn = financialReturn,
        productivityGain = productivityGain, costReduction = costReduction, division = division.name,
        guidelineId = guidelineId, responsibleId = responsibleId, note = note, version = version
    )
}
