package com.aguiabranca.app.feature.projects.data

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.Query
import com.aguiabranca.app.core.data.dto.ProjectDto
import com.aguiabranca.app.core.data.dto.ProjectUpdateDto
import com.aguiabranca.app.core.data.mapper.toDomain
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.data.snapshotsAsFlow
import com.aguiabranca.app.core.domain.ProjectInput
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.tasks.await
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class FirestoreProjectsRepository @Inject constructor(
    private val firestore: FirebaseFirestore
) : ProjectsRepository {

    private val col get() = firestore.collection("projects")

    override fun observeAll(): Flow<List<Project>> =
        col.orderBy("updatedAt", Query.Direction.DESCENDING).snapshotsAsFlow().map { snap ->
            snap.documents.mapNotNull { d -> d.toObject(ProjectDto::class.java)?.toDomain(d.id) }
        }

    override fun observe(id: String): Flow<Project?> =
        col.document(id).snapshotsAsFlow().map { snap ->
            if (!snap.exists()) null
            else snap.toObject(ProjectDto::class.java)?.toDomain(snap.id)
        }

    override fun observeUpdates(projectId: String): Flow<List<ProjectUpdate>> =
        col.document(projectId).collection("updates")
            .orderBy("createdAt", Query.Direction.DESCENDING)
            .snapshotsAsFlow()
            .map { snap ->
                snap.documents.mapNotNull { d ->
                    d.toObject(ProjectUpdateDto::class.java)?.toDomain(d.id)
                }
            }

    override fun observeByGuideline(guidelineId: String): Flow<List<Project>> =
        col.whereEqualTo("guidelineId", guidelineId).snapshotsAsFlow().map { snap ->
            snap.documents.mapNotNull { d -> d.toObject(ProjectDto::class.java)?.toDomain(d.id) }
        }

    override suspend fun create(
        input: ProjectInput,
        creatorManagerId: String,
        creatorManagerName: String,
        originatingIdeaId: String?
    ): Outcome<String> = runOutcome {
        val ref = col.document()
        val now = Timestamp.now()
        val dto = ProjectDto(
            title = input.title.trim(),
            description = input.description.trim(),
            stage = input.stage.name,
            statusText = input.statusText.trim(),
            investment = input.investment,
            targetDate = input.targetDate?.let { Timestamp(java.util.Date(it)) },
            financialReturn = input.financialReturn,
            productivityGain = input.productivityGain,
            costReduction = input.costReduction,
            division = input.division.name,
            guidelineId = input.guidelineId,
            creatorManagerId = creatorManagerId,
            originatingIdeaId = originatingIdeaId,
            createdAt = now,
            updatedAt = now
        )
        val firstUpdate = ProjectUpdateDto(
            authorId = creatorManagerId,
            authorName = creatorManagerName,
            note = if (originatingIdeaId != null) "Criado automaticamente a partir de ideia $originatingIdeaId" else "Projeto criado",
            changes = emptyList(),
            createdAt = now
        )
        firestore.runTransaction { tx ->
            tx.set(ref, dto)
            tx.set(ref.collection("updates").document(), firstUpdate)
            null
        }.await()
        ref.id
    }

    override suspend fun update(
        id: String,
        input: ProjectInput,
        editorManagerId: String,
        editorManagerName: String,
        note: String
    ): Outcome<Unit> = runOutcome {
        val ref = col.document(id)
        val updatesRef = ref.collection("updates").document()
        firestore.runTransaction { tx ->
            val snap = tx.get(ref)
            if (!snap.exists()) throw NoSuchElementException("project $id")
            val prior = snap.toObject(ProjectDto::class.java) ?: ProjectDto()
            val changes = mutableListOf<Map<String, Any?>>()
            fun add(field: String, from: Any?, to: Any?) {
                if (from != to) changes += mapOf("field" to field, "from" to from, "to" to to)
            }
            add("title", prior.title, input.title)
            add("description", prior.description, input.description)
            add("stage", prior.stage, input.stage.name)
            add("statusText", prior.statusText, input.statusText)
            add("investment", prior.investment, input.investment)
            add("financialReturn", prior.financialReturn, input.financialReturn)
            add("productivityGain", prior.productivityGain, input.productivityGain)
            add("costReduction", prior.costReduction, input.costReduction)
            add("guidelineId", prior.guidelineId, input.guidelineId)
            val priorTarget = prior.targetDate?.toDate()?.time
            add("targetDate", priorTarget, input.targetDate)

            val now = Timestamp.now()
            tx.update(
                ref,
                mapOf(
                    "title" to input.title.trim(),
                    "description" to input.description.trim(),
                    "stage" to input.stage.name,
                    "statusText" to input.statusText.trim(),
                    "investment" to input.investment,
                    "targetDate" to input.targetDate?.let { Timestamp(java.util.Date(it)) },
                    "financialReturn" to input.financialReturn,
                    "productivityGain" to input.productivityGain,
                    "costReduction" to input.costReduction,
                    "guidelineId" to input.guidelineId,
                    "updatedAt" to now
                )
            )
            tx.set(
                updatesRef,
                ProjectUpdateDto(
                    authorId = editorManagerId,
                    authorName = editorManagerName,
                    note = note,
                    changes = changes,
                    createdAt = now
                )
            )
            null
        }.await()
        Unit
    }

    override suspend fun delete(id: String): Outcome<Unit> = runOutcome {
        col.document(id).delete().await()
        Unit
    }
}
