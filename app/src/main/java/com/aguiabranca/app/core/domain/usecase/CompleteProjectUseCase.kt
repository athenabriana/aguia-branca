package com.aguiabranca.app.core.domain.usecase

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FirebaseFirestore
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.util.Analytics
import kotlinx.coroutines.tasks.await
import javax.inject.Inject

class CompleteProjectUseCase @Inject constructor(
    private val firestore: FirebaseFirestore,
    private val analytics: Analytics
) {
    suspend operator fun invoke(projectId: String): Outcome<Unit> = runOutcome {
        val projectRef = firestore.collection("projects").document(projectId)
        val now = Timestamp.now()
        var hasIdea = false
        firestore.runTransaction { tx ->
            val pSnap = tx.get(projectRef)
            if (!pSnap.exists()) throw NoSuchElementException("project $projectId")
            val originatingIdeaId = pSnap.getString("originatingIdeaId")
            if (originatingIdeaId.isNullOrBlank()) return@runTransaction null
            hasIdea = true
            val ideaRef = firestore.collection("ideas").document(originatingIdeaId)
            val ideaSnap = tx.get(ideaRef)
            if (!ideaSnap.exists()) return@runTransaction null
            val currentStatus = ideaSnap.getString("status") ?: IdeaStatus.SUBMETIDA.name
            if (currentStatus == IdeaStatus.IMPLEMENTADA.name) return@runTransaction null
            tx.update(ideaRef, "status", IdeaStatus.IMPLEMENTADA.name)
            val authorId = ideaSnap.getString("authorId") ?: return@runTransaction null
            val authorRef = firestore.collection("users").document(authorId)
            val authorSnap = tx.get(authorRef)
            val currentPoints = authorSnap.getLong("points") ?: 0L
            val newPoints = (currentPoints + 200L).coerceAtLeast(0L)
            tx.update(authorRef, "points", newPoints)
            null
        }.await()
        analytics.logProjectCompleted(projectId, hasOriginatingIdea = hasIdea)
        Unit
    }
}
