package com.aguiabranca.app.core.network

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.stringPreferencesKey
import app.cash.turbine.test
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import kotlin.time.Duration.Companion.seconds

@OptIn(ExperimentalCoroutinesApi::class)
class PollingAndStoreTests {

    @Test fun `pollingFlow emits immediately and then at every interval while collected`() = runTest {
        var calls = 0
        val bus = RefreshBus()
        pollingFlow(bus, setOf(RefreshKeys.IDEAS), interval = 15.seconds) { ++calls }.test {
            assertEquals(1, awaitItem())                    // imediato
            advanceTimeBy(14_000); runCurrent(); expectNoEvents()
            advanceTimeBy(1_100); assertEquals(2, awaitItem())  // 15 s
            advanceTimeBy(15_000); assertEquals(3, awaitItem())
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `invalidate forces an immediate refetch only for the subscribed keys`() = runTest {
        var calls = 0
        val bus = RefreshBus()
        pollingFlow(bus, setOf(RefreshKeys.PROJECTS), interval = 15.seconds) { ++calls }.test {
            assertEquals(1, awaitItem())
            bus.invalidate(RefreshKeys.IDEAS); runCurrent(); expectNoEvents()      // outra chave: ignora
            bus.invalidate(RefreshKeys.PROJECTS); assertEquals(2, awaitItem())     // a sua: refaz já
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `a failing fetch does not end the flow and keeps polling`() = runTest {
        var calls = 0
        val errors = mutableListOf<Throwable>()
        pollingFlow(RefreshBus(), setOf("k"), interval = 15.seconds, onError = { errors += it }) {
            if (++calls == 2) throw java.io.IOException("sem rede") else calls
        }.test {
            assertEquals(1, awaitItem())
            advanceTimeBy(15_100); runCurrent()                 // 2ª tentativa falha: sem emissão
            advanceTimeBy(15_000); assertEquals(3, awaitItem()) // 3ª volta ao normal
            assertEquals(1, errors.size)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `a legitimate null result is emitted, not dropped`() = runTest {
        pollingFlow<String?>(RefreshBus(), setOf("k")) { null }.test {
            assertNull(awaitItem())
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `no fetching happens when nobody collects`() = runTest {
        var calls = 0
        val flow = pollingFlow(RefreshBus(), setOf("k")) { ++calls }
        advanceTimeBy(60_000); runCurrent()
        assertEquals(0, calls)
        flow.first()
        assertEquals(1, calls)
    }

    @Test fun `tokens are encrypted at rest and cleared on logout`() = runBlocking {
        val file = File.createTempFile("session", ".preferences_pb").apply { delete() }
        val ds = PreferenceDataStoreFactory.create(scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)) { file }
        val store = DataStoreTokenStore(ds, ReversingCipher())

        assertNull(store.get())
        store.save(Tokens("segredo-access", "segredo-refresh"))

        val raw = ds.data.first().asMap().mapKeys { it.key.name }.mapValues { it.value.toString() }
        assertTrue(raw.values.none { it.contains("segredo-access") || it.contains("segredo-refresh") })
        assertEquals(setOf("access_token_enc", "refresh_token_enc"), raw.keys)
        assertFalse(file.readText(Charsets.ISO_8859_1).contains("segredo-access"))
        assertEquals(Tokens("segredo-access", "segredo-refresh"), store.get())

        store.clear()
        assertNull(store.get())
        assertTrue(ds.data.first().asMap().isEmpty())
        file.delete(); Unit
    }

    @Test fun `undecryptable stored value is treated as no session`() = runBlocking {
        val file = File.createTempFile("session2", ".preferences_pb").apply { delete() }
        val ds = PreferenceDataStoreFactory.create(scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)) { file }
        ds.updateData { p -> p.toMutablePreferences().apply {
            this[stringPreferencesKey("access_token_enc")] = "lixo"; this[stringPreferencesKey("refresh_token_enc")] = "lixo"
        } }

        assertNull(DataStoreTokenStore(ds, ReversingCipher()).get())
        file.delete(); Unit
    }
}
