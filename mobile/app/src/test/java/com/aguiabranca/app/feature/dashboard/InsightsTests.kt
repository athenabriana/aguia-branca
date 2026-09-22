package com.aguiabranca.app.feature.dashboard

import app.cash.turbine.test
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.InsightPriority
import com.aguiabranca.app.core.domain.model.InsightRecommendation
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.api.ReportsApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.core.network.problemJson
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.feature.dashboard.data.RemoteReportsRepository
import com.aguiabranca.app.feature.dashboard.ui.InsightsViewModel
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

private fun insights(fromCache: Boolean = false, summary: String = "Resumo executivo.") = Insights(
    summary = summary, highlights = listOf("ROI de 36%"), risks = listOf("Projeto atrasado"),
    recommendations = listOf(InsightRecommendation("Escalar piloto", "Detalhe", InsightPriority.ALTA, "g1")),
    generatedAt = 1_790_000_000_000L, model = "gemini-3.1-flash-lite", fromCache = fromCache
)

@OptIn(ExperimentalCoroutinesApi::class)
class InsightsViewModelTests {
    private val repo = FakeReportsRepository()
    private val filters = DashboardFilters(Period.THIS_YEAR, Division.LOGISTICA)

    @Before fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `idle then loading then success with the active filters`() = runBlocking {
        val gate = CompletableDeferred<Unit>()
        repo.insightsScript = { gate.await(); Outcome.Success(insights()) }
        val vm = InsightsViewModel(repo)

        vm.ui.test {
            assertEquals(UiState.Idle, awaitItem().state)
            vm.generate(filters)
            val loading = awaitItem()
            assertEquals(UiState.Loading, loading.state)
            assertEquals(filters, loading.generatedFor)
            gate.complete(Unit)
            val done = awaitItem()
            assertEquals("Resumo executivo.", (done.state as UiState.Success).data.summary)
            assertEquals(filters, done.generatedFor)
            cancelAndIgnoreRemainingEvents()
        }
        assertEquals(FakeReportsRepository.InsightsCall(filters, null, false), repo.insightsCalls.single())
    }

    @Test fun `AI unavailable shows only the error and retry generates again`() = runBlocking {
        var down = true
        repo.insightsScript = {
            if (down) Outcome.Failure(DomainError.ServiceUnavailable("AI_UNAVAILABLE", "O serviço de IA está indisponível no momento."))
            else Outcome.Success(insights())
        }
        val vm = InsightsViewModel(repo)

        vm.generate(filters)
        val error = vm.ui.value.state
        assertTrue("nenhum conteúdo é mostrado na falha", error is UiState.Error && (error.error as DomainError.ServiceUnavailable).code == "AI_UNAVAILABLE")

        down = false
        vm.generate(filters)
        assertTrue(vm.ui.value.state is UiState.Success<*>)
        assertEquals(2, repo.insightsCalls.size)
    }

    @Test fun `invalid response and rate limit are surfaced as their own errors`() = runBlocking {
        val vm = InsightsViewModel(repo)

        repo.insightsScript = { Outcome.Failure(DomainError.ServiceUnavailable("AI_INVALID_RESPONSE", "A IA devolveu uma resposta inválida.")) }
        vm.generate(filters)
        assertTrue((vm.ui.value.state as UiState.Error).error is DomainError.ServiceUnavailable)

        repo.insightsScript = { Outcome.Failure(DomainError.TooManyRequests("Limite diário atingido.", 3600)) }
        vm.generate(filters)
        assertEquals(DomainError.TooManyRequests("Limite diário atingido.", 3600), (vm.ui.value.state as UiState.Error).error)
    }

    @Test fun `update sends refresh true and shows the fresh result, not the cached one`() = runBlocking {
        repo.insightsScript = { call -> Outcome.Success(insights(fromCache = !call.refresh, summary = if (call.refresh) "Novo" else "Antigo")) }
        val vm = InsightsViewModel(repo)

        vm.generate(filters)
        assertTrue((vm.ui.value.state as UiState.Success).data.fromCache)

        vm.generate(filters, refresh = true)
        val fresh = (vm.ui.value.state as UiState.Success).data
        assertEquals("Novo", fresh.summary)
        assertTrue(!fresh.fromCache)
        assertEquals(listOf(false, true), repo.insightsCalls.map { it.refresh })
    }

    @Test fun `a second tap while loading does not fire a second request`() = runBlocking {
        val gate = CompletableDeferred<Unit>()
        repo.insightsScript = { gate.await(); Outcome.Success(insights()) }
        val vm = InsightsViewModel(repo)

        vm.generate(filters); vm.generate(filters); vm.generate(filters)
        gate.complete(Unit)

        assertEquals(1, repo.insightsCalls.size)
    }

    @Test fun `the result remembers which filters produced it so the card can flag a stale one`() = runBlocking {
        repo.insightsScript = { Outcome.Success(insights()) }
        val vm = InsightsViewModel(repo)

        vm.generate(filters)

        assertEquals(filters, vm.ui.value.generatedFor)
        assertTrue(vm.ui.value.generatedFor != DashboardFilters(Period.ALL, null))
        vm.reset()
        assertEquals(UiState.Idle, vm.ui.value.state)
    }
}

class RemoteInsightsTests {
    private val h = NetworkHarness()
    private val repo = RemoteReportsRepository(h.api(ReportsApi::class.java), RefreshBus())

    @After fun tearDown() = h.shutdown()

    private val body = """{"summary":"O portfólio apresenta ROI de 36,73%.","highlights":["ROI de 36,73%"],"risks":["Um projeto atrasado"],
        "recommendations":[{"title":"Revisar","detail":"Detalhe","priority":"ALTA","relatedGuidelineId":"g1"},{"title":"Monitorar","detail":"d","priority":"BAIXA","relatedGuidelineId":null}],
        "generatedAt":"2026-09-21T22:15:08.47Z","model":"gemini-3.1-flash-lite","fromCache":true}"""

    @Test fun `maps the structured insight and sends filters, guideline and refresh in the body`() = runBlocking {
        h.server.enqueue(MockResponse().json(body))

        val outcome = repo.generateInsights(DashboardFilters(Period.LAST_QUARTER, Division.COMERCIO), guidelineId = "g1", refresh = true)

        val insight = (outcome as Outcome.Success).value
        assertEquals(listOf("ALTA", "BAIXA"), insight.recommendations.map { it.priority.name })
        assertEquals("g1", insight.recommendations[0].relatedGuidelineId)
        assertTrue(insight.fromCache && insight.generatedAt > 0)
        assertEquals("gemini-3.1-flash-lite", insight.model)
        val request = h.server.takeRequest()
        assertEquals("POST /api/v1/reports/insights", "${request.method} ${request.path}")
        assertEquals("""{"period":"LAST_QUARTER","division":"COMERCIO","guidelineId":"g1","refresh":true}""", request.body.readUtf8())
    }

    @Test fun `AI outages and limits map to friendly domain errors carrying the server message`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(503, "AI_UNAVAILABLE", "O serviço de IA está indisponível no momento. Tente novamente em instantes."), 503))
        h.server.enqueue(MockResponse().json(problemJson(502, "AI_INVALID_RESPONSE", "A IA devolveu uma resposta inválida. Tente novamente."), 502))
        h.server.enqueue(MockResponse().json(problemJson(429, "RATE_LIMITED", "O limite diário de gerações de insights foi atingido."), 429).setHeader("Retry-After", "7200"))

        val unavailable = (repo.generateInsights(DashboardFilters()) as Outcome.Failure).error
        val invalid = (repo.generateInsights(DashboardFilters()) as Outcome.Failure).error
        val limited = (repo.generateInsights(DashboardFilters()) as Outcome.Failure).error

        assertEquals("AI_UNAVAILABLE", (unavailable as DomainError.ServiceUnavailable).code)
        assertEquals("AI_INVALID_RESPONSE", (invalid as DomainError.ServiceUnavailable).code)
        assertEquals(DomainError.TooManyRequests("O limite diário de gerações de insights foi atingido.", 7200), limited)
    }
}
