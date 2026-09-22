package com.aguiabranca.app.core.network

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/** Sinaliza que a sessão terminou por motivo externo (refresh recusado): a UI volta ao Login. */
class SessionEvents {
    private val _expired = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val expired: SharedFlow<Unit> = _expired.asSharedFlow()
    fun notifyExpired() { _expired.tryEmit(Unit) }
}
