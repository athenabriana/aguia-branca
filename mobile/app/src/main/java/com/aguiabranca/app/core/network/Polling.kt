package com.aguiabranca.app.core.network

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.mapLatest
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.onStart
import kotlin.time.Duration
import kotlin.time.Duration.Companion.seconds

/** Chaves de invalidação (o que cada escrita precisa fazer as telas rebuscarem). */
object RefreshKeys {
    const val GUIDELINES = "guidelines"
    const val IDEAS = "ideas"
    const val PROJECTS = "projects"
    const val REPORTS = "reports"
    const val ME = "me"
    const val RANKING = "ranking"
}

/** Barramento de invalidação: `invalidate(key)` faz todo `pollingFlow` com aquela chave rebuscar **na hora**. */
class RefreshBus {
    private val _events = MutableSharedFlow<String>(extraBufferCapacity = 64)
    val events: SharedFlow<String> = _events.asSharedFlow()

    fun invalidate(vararg keys: String) { keys.forEach { _events.tryEmit(it) } }
}

val DEFAULT_POLL_INTERVAL: Duration = 15.seconds

/**
 * Substitui o "tempo real" do Firestore por polling (R2-08.3): emite **imediatamente** ao ser coletado, de novo a cada
 * [interval] e sempre que o [bus] invalidar uma das [keys]. Uma falha de rede/servidor não encerra o fluxo: mantém o último
 * valor e tenta no próximo ciclo (a falha é entregue a [onError]).
 */
fun <T> pollingFlow(
    bus: RefreshBus,
    keys: Set<String>,
    interval: Duration = DEFAULT_POLL_INTERVAL,
    onError: (Throwable) -> Unit = {},
    fetch: suspend () -> T
): Flow<T> {
    val ticker = flow { while (true) { delay(interval); emit(Unit) } }
    val invalidations = bus.events.filter { it in keys }.map { }
    return merge(ticker, invalidations)
        .onStart { emit(Unit) }
        .mapLatest {
            try { Result.success(fetch()) }
            catch (c: CancellationException) { throw c }
            catch (t: Throwable) { onError(t); Result.failure(t) }
        }
        // isSuccess (e não getOrNull): um resultado legítimo `null` (ex.: registro inexistente) também deve ser emitido.
        .filter { it.isSuccess }
        .map { it.getOrThrow() }
}
