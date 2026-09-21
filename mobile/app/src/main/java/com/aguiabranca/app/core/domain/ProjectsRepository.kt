package com.aguiabranca.app.core.domain

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import kotlinx.coroutines.flow.Flow

data class ProjectInput(
    val title: String,
    val description: String,
    val stage: ProjectStage,
    val statusText: String,
    val investment: Double,
    val targetDate: Long?,
    val financialReturn: Double,
    val productivityGain: Double,
    val costReduction: Double,
    val division: Division,
    val guidelineId: String?,
    val responsibleId: String? = null,
    val responsibleName: String? = null
)

interface ProjectsRepository {
    fun observeAll(): Flow<List<Project>>
    fun observe(id: String): Flow<Project?>
    fun observeUpdates(projectId: String): Flow<List<ProjectUpdate>>
    fun observeByGuideline(guidelineId: String): Flow<List<Project>>

    suspend fun create(
        input: ProjectInput,
        creatorManagerId: String,
        creatorManagerName: String,
        originatingIdeaId: String? = null
    ): Outcome<String>

    suspend fun update(
        id: String,
        input: ProjectInput,
        editorManagerId: String,
        editorManagerName: String,
        note: String
    ): Outcome<Unit>

    suspend fun delete(id: String): Outcome<Unit>
}
