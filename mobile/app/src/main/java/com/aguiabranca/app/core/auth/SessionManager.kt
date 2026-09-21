package com.aguiabranca.app.core.auth

import com.google.firebase.auth.FirebaseAuth
import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.core.domain.model.AuthSession
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.tasks.await
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SessionManager @Inject constructor(
    private val auth: FirebaseAuth,
    private val usersRepo: UsersRepository
) {
    private val scope = CoroutineScope(SupervisorJob())

    private val authUidFlow = callbackFlow<String?> {
        val listener = FirebaseAuth.AuthStateListener { trySend(it.currentUser?.uid) }
        auth.addAuthStateListener(listener)
        awaitClose { auth.removeAuthStateListener(listener) }
    }

    val currentUser: StateFlow<AuthSession?> = authUidFlow
        .flatMapLatest { uid ->
            if (uid == null) flowOf(null) else usersRepo.observe(uid).map { user -> user?.let(AuthSession::from) }
        }
        .stateIn(scope, SharingStarted.WhileSubscribed(5_000), null)

    suspend fun signIn(email: String, password: String) {
        auth.signInWithEmailAndPassword(email, password).await()
    }

    fun signOut() {
        auth.signOut()
    }
}
