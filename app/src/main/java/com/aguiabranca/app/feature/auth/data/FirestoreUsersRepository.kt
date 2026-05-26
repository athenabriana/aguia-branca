package com.aguiabranca.app.feature.auth.data

import com.google.firebase.Timestamp
import com.google.firebase.firestore.FieldValue
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.Query
import com.aguiabranca.app.core.data.dto.UserDto
import com.aguiabranca.app.core.data.mapper.toDomain
import com.aguiabranca.app.core.data.snapshotsAsFlow
import com.aguiabranca.app.core.data.runOutcome
import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.tasks.await
import java.util.Calendar
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class FirestoreUsersRepository @Inject constructor(
    private val firestore: FirebaseFirestore
) : UsersRepository {

    private val col get() = firestore.collection("users")

    override fun observe(uid: String): Flow<User?> =
        col.document(uid).snapshotsAsFlow().map { snap ->
            if (!snap.exists()) null
            else snap.toObject(UserDto::class.java)?.toDomain(snap.id)
        }

    override suspend fun ensureProfileExists(
        uid: String,
        email: String,
        name: String,
        role: Role,
        division: Division
    ): Outcome<Unit> = runOutcome {
        val ref = col.document(uid)
        val snap = ref.get().await()
        if (!snap.exists()) {
            val dto = UserDto(
                name = name,
                email = email,
                role = role.name,
                division = division.name,
                points = 0L,
                badges = emptyList(),
                createdAt = Timestamp.now()
            )
            ref.set(dto).await()
        }
        Unit
    }

    override suspend fun topByPointsThisMonth(limit: Int): Outcome<List<User>> = runOutcome {
        val startOfMonth = Calendar.getInstance().apply {
            set(Calendar.DAY_OF_MONTH, 1)
            set(Calendar.HOUR_OF_DAY, 0); set(Calendar.MINUTE, 0); set(Calendar.SECOND, 0); set(Calendar.MILLISECOND, 0)
        }
        val q = col
            .whereEqualTo("role", Role.OPERADOR.name)
            .orderBy("points", Query.Direction.DESCENDING)
            .limit(limit.toLong())
        val snap = q.get().await()
        snap.documents.mapNotNull { d ->
            d.toObject(UserDto::class.java)?.toDomain(d.id)
        }
    }

    suspend fun creditPoints(uid: String, delta: Long): Outcome<Unit> = runOutcome {
        firestore.runTransaction { tx ->
            val ref = col.document(uid)
            val snap = tx.get(ref)
            val current = (snap.getLong("points") ?: 0L)
            val next = (current + delta).coerceAtLeast(0L)
            tx.update(ref, "points", next)
        }.await()
        Unit
    }
}
