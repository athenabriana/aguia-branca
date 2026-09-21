package com.aguiabranca.app.feature.ideas.data

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FieldValue
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.Query
import com.aguiabranca.app.core.data.dto.IdeaDto
import com.aguiabranca.app.core.data.dto.UserDto
import com.aguiabranca.app.core.data.mapper.toDomain
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.data.snapshotsAsFlow
import com.aguiabranca.app.core.domain.CreateIdeaInput
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.UpdateIdeaInput
import com.aguiabranca.app.core.domain.badge.BadgeEvaluator
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.tasks.await
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class FirestoreIdeasRepository @Inject constructor(
    private val firestore: FirebaseFirestore
) : IdeasRepository {

    private val col get() = firestore.collection("ideas")
    private val users get() = firestore.collection("users")

    private fun docs(snap: com.google.firebase.firestore.QuerySnapshot): List<Idea> =
        snap.documents.mapNotNull { d -> d.toObject(IdeaDto::class.java)?.toDomain(d.id) }

    override fun observeAll(): Flow<List<Idea>> =
        col.orderBy("createdAt", Query.Direction.DESCENDING).snapshotsAsFlow().map(::docs)

    override fun observeByAuthor(authorId: String): Flow<List<Idea>> =
        col.whereEqualTo("authorId", authorId)
            .orderBy("createdAt", Query.Direction.DESCENDING)
            .snapshotsAsFlow().map(::docs)

    override fun observeForCuration(): Flow<List<Idea>> =
        col.whereIn("status", listOf(IdeaStatus.SUBMETIDA.name, IdeaStatus.EM_ANALISE.name))
            .snapshotsAsFlow().map(::docs)

    override fun observeByGuideline(guidelineId: String): Flow<List<Idea>> =
        col.whereEqualTo("guidelineId", guidelineId).snapshotsAsFlow().map(::docs)

    override fun observe(id: String): Flow<Idea?> =
        col.document(id).snapshotsAsFlow().map { snap ->
            if (!snap.exists()) null
            else snap.toObject(IdeaDto::class.java)?.toDomain(snap.id)
        }


    override suspend fun createIdea(input: CreateIdeaInput): Outcome<String> = runOutcome {
        val ideaRef = col.document()
        val userRef = users.document(input.authorId)
        val pointsDelta = if (input.guidelineId != null) 15L else 10L
        firestore.runTransaction { tx ->
            val userSnap = tx.get(userRef)
            val currentPoints = userSnap.getLong("points") ?: 0L
            val newPoints = (currentPoints + pointsDelta).coerceAtLeast(0L)

            val dto = IdeaDto(
                title = input.title.trim(),
                description = input.description.trim(),
                category = input.category.trim().replaceFirstChar { it.uppercase() },
                division = input.division.name,
                guidelineId = input.guidelineId,
                authorId = input.authorId,
                authorName = input.authorName,
                status = IdeaStatus.SUBMETIDA.name,
                ice = null,
                reviewerId = null,
                reviewComment = null,
                createdAt = Timestamp.now(),
                reviewedAt = null
            )
            tx.set(ideaRef, dto)
            tx.update(userRef, "points", newPoints)
            null
        }.await()
        ideaRef.id
    }

    override suspend fun updateIdea(id: String, input: UpdateIdeaInput): Outcome<Unit> = runOutcome {
        val ref = col.document(id)
        val snap = ref.get().await()
        if (!snap.exists()) throw NoSuchElementException("idea $id not found")
        val status = snap.getString("status") ?: IdeaStatus.SUBMETIDA.name
        if (status != IdeaStatus.SUBMETIDA.name) {
            throw IllegalStateException("Só é possível editar ideias em SUBMETIDA")
        }
        ref.update(
            mapOf(
                "title" to input.title.trim(),
                "description" to input.description.trim(),
                "category" to input.category.trim().replaceFirstChar { it.uppercase() },
                "division" to input.division.name,
                "guidelineId" to input.guidelineId
            )
        ).await()
        Unit
    }

    override suspend fun deleteIdea(id: String, authorId: String): Outcome<Unit> = runOutcome {
        val ideaRef = col.document(id)
        val userRef = users.document(authorId)
        firestore.runTransaction { tx ->
            val ideaSnap = tx.get(ideaRef)
            if (!ideaSnap.exists()) throw NoSuchElementException("idea $id")
            val status = ideaSnap.getString("status") ?: IdeaStatus.SUBMETIDA.name
            if (status != IdeaStatus.SUBMETIDA.name) {
                throw IllegalStateException("Apagar ideia exige status SUBMETIDA")
            }
            val hadGuideline = !ideaSnap.getString("guidelineId").isNullOrBlank()
            val delta = if (hadGuideline) -15L else -10L
            val userSnap = tx.get(userRef)
            val currentPoints = userSnap.getLong("points") ?: 0L
            val newPoints = (currentPoints + delta).coerceAtLeast(0L)
            tx.update(userRef, "points", newPoints)
            tx.delete(ideaRef)
            null
        }.await()
        Unit
    }

    override suspend fun saveIce(id: String, ice: Ice, reviewerId: String): Outcome<Unit> = runOutcome {
        if (!ice.isComplete) throw IllegalArgumentException("ICE incompleto")
        val ref = col.document(id)
        firestore.runTransaction { tx ->
            val snap = tx.get(ref)
            if (!snap.exists()) throw NoSuchElementException("idea $id")
            val currentStatus = snap.getString("status") ?: IdeaStatus.SUBMETIDA.name
            val newStatus = if (currentStatus == IdeaStatus.SUBMETIDA.name) IdeaStatus.EM_ANALISE.name else currentStatus
            tx.update(
                ref,
                mapOf(
                    "ice" to mapOf(
                        "impact" to ice.impact,
                        "confidence" to ice.confidence,
                        "ease" to ice.ease,
                        "score" to ice.score
                    ),
                    "status" to newStatus,
                    "reviewerId" to reviewerId
                )
            )
            null
        }.await()
        Unit
    }

    override suspend fun rejectIdea(id: String, reviewerId: String, comment: String): Outcome<Unit> {
        if (comment.isBlank()) return Outcome.Failure(DomainError.ValidationFailed("reviewComment", "obrigatório"))
        return runOutcome {
            col.document(id).update(
                mapOf(
                    "status" to IdeaStatus.REJEITADA.name,
                    "reviewerId" to reviewerId,
                    "reviewComment" to comment.trim(),
                    "reviewedAt" to Timestamp.now()
                )
            ).await()
            Unit
        }
    }
}
