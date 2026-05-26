package com.aguiabranca.app.feature.guidelines.data

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.Query
import com.aguiabranca.app.core.data.dto.GuidelineDto
import com.aguiabranca.app.core.data.mapper.toDomain
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.data.snapshotsAsFlow
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Pillar
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.tasks.await
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class FirestoreGuidelinesRepository @Inject constructor(
    private val firestore: FirebaseFirestore
) : GuidelinesRepository {

    private val col get() = firestore.collection("strategicGuidelines")

    override fun observeAll(): Flow<List<Guideline>> =
        col.orderBy("updatedAt", Query.Direction.DESCENDING)
            .snapshotsAsFlow()
            .map { snap ->
                snap.documents.mapNotNull { d ->
                    d.toObject(GuidelineDto::class.java)?.toDomain(d.id)
                }
            }

    override fun observe(id: String): Flow<Guideline?> =
        col.document(id).snapshotsAsFlow().map { snap ->
            if (!snap.exists()) null
            else snap.toObject(GuidelineDto::class.java)?.toDomain(snap.id)
        }

    override suspend fun create(
        title: String,
        description: String,
        pillar: Pillar,
        authorId: String,
        authorName: String
    ): Outcome<String> = runOutcome {
        val now = Timestamp.now()
        val dto = GuidelineDto(
            title = title.trim(),
            description = description.trim(),
            pillar = pillar.name,
            authorId = authorId,
            authorName = authorName,
            createdAt = now,
            updatedAt = now
        )
        col.add(dto).await().id
    }

    override suspend fun update(
        id: String,
        title: String,
        description: String,
        pillar: Pillar
    ): Outcome<Unit> = runOutcome {
        col.document(id).update(
            mapOf(
                "title" to title.trim(),
                "description" to description.trim(),
                "pillar" to pillar.name,
                "updatedAt" to Timestamp.now()
            )
        ).await()
        Unit
    }

    override suspend fun delete(id: String): Outcome<Unit> = runOutcome {
        col.document(id).delete().await()
        Unit
    }
}
