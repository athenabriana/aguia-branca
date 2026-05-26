package com.aguiabranca.app.core.data

import com.google.firebase.firestore.DocumentSnapshot
import com.google.firebase.firestore.FirebaseFirestoreException
import com.google.firebase.firestore.Query
import com.google.firebase.firestore.QuerySnapshot
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

fun Query.snapshotsAsFlow(): Flow<QuerySnapshot> = callbackFlow {
    val reg = addSnapshotListener { value, error ->
        if (error != null) close(error)
        else if (value != null) trySend(value)
    }
    awaitClose { reg.remove() }
}

fun com.google.firebase.firestore.DocumentReference.snapshotsAsFlow(): Flow<DocumentSnapshot> = callbackFlow {
    val reg = addSnapshotListener { value, error ->
        if (error != null) close(error)
        else if (value != null) trySend(value)
    }
    awaitClose { reg.remove() }
}

fun Throwable.toDomainError(): DomainError = when (this) {
    is FirebaseFirestoreException -> when (code) {
        FirebaseFirestoreException.Code.UNAVAILABLE -> DomainError.NetworkUnavailable(this)
        FirebaseFirestoreException.Code.PERMISSION_DENIED -> DomainError.PermissionDenied(message)
        FirebaseFirestoreException.Code.NOT_FOUND -> DomainError.NotFound("documento")
        else -> DomainError.Unknown(this)
    }
    else -> DomainError.Unknown(this)
}

inline fun <T> runOutcome(block: () -> T): Outcome<T> = try {
    Outcome.Success(block())
} catch (t: Throwable) {
    Outcome.Failure(t.toDomainError())
}
