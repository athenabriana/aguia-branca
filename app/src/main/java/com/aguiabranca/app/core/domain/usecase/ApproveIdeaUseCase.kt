package com.aguiabranca.app.core.domain.usecase

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FirebaseFirestore
import com.aguiabranca.app.core.data.dto.IdeaDto
import com.aguiabranca.app.core.data.dto.ProjectDto
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.util.Analytics
import kotlinx.coroutines.tasks.await
import javax.inject.Inject

class ApproveIdeaUseCase @Inject constructor(
    private val firestore: FirebaseFirestore,
    private val analytics: Analytics
) {
    suspend operator fun invoke(ideaId: String, reviewerId: String, reviewerName: String): Outcome<String> {
        val ideaRef = firestore.collection("ideas").document(ideaId)

        // Bloqueio de auto-aprovação. Defesa-em-profundidade junto com as regras Firestore
        // e com a UI (que esconde o botão Aprovar para o autor).
        val preCheck = runCatching { ideaRef.get().await() }.getOrNull()
        if (preCheck != null && preCheck.exists() && preCheck.getString("authorId") == reviewerId) {
            return Outcome.Failure(DomainError.PermissionDenied("Você não pode aprovar a própria ideia."))
        }

        return runOutcome {
            val projectRef = firestore.collection("projects").document()
            val projectUpdateRef = projectRef.collection("updates").document()
            val now = Timestamp.now()

            val projectId = firestore.runTransaction { tx ->
                val ideaSnap = tx.get(ideaRef)
                if (!ideaSnap.exists()) throw NoSuchElementException("idea $ideaId")
                val currentStatus = ideaSnap.getString("status") ?: IdeaStatus.SUBMETIDA.name
                if (currentStatus == IdeaStatus.APROVADA.name || currentStatus == IdeaStatus.IMPLEMENTADA.name) {
                    return@runTransaction projectRef.id
                }
                val ideaDto = ideaSnap.toObject(IdeaDto::class.java) ?: throw IllegalStateException("malformed idea")
                val authorId = ideaDto.authorId
                val authorRef = firestore.collection("users").document(authorId)
                val authorSnap = tx.get(authorRef)

                tx.update(
                    ideaRef,
                    mapOf(
                        "status" to IdeaStatus.APROVADA.name,
                        "reviewerId" to reviewerId,
                        "reviewedAt" to now
                    )
                )
                tx.set(
                    projectRef,
                    ProjectDto(
                        title = "PROJ: ${ideaDto.title}",
                        description = ideaDto.description,
                        stage = ProjectStage.PLANEJAMENTO.name,
                        statusText = "Iniciado a partir de aprovação",
                        investment = 0.0,
                        targetDate = null,
                        financialReturn = 0.0,
                        productivityGain = 0.0,
                        costReduction = 0.0,
                        division = ideaDto.division,
                        guidelineId = ideaDto.guidelineId,
                        creatorManagerId = reviewerId,
                        originatingIdeaId = ideaId,
                        createdAt = now,
                        updatedAt = now
                    )
                )
                tx.set(
                    projectUpdateRef,
                    mapOf(
                        "authorId" to reviewerId,
                        "authorName" to reviewerName,
                        "note" to "Criado automaticamente a partir da ideia: ${ideaDto.title}",
                        "changes" to emptyList<Map<String, Any?>>(),
                        "createdAt" to now
                    )
                )
                val currentPoints = authorSnap.getLong("points") ?: 0L
                val newPoints = (currentPoints + 50L).coerceAtLeast(0L)
                tx.update(authorRef, "points", newPoints)
                projectRef.id
            }.await()

            analytics.logIdeaApproved(projectId)
            projectId
        }
    }
}
