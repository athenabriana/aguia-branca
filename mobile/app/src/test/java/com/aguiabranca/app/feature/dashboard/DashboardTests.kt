package com.aguiabranca.app.feature.dashboard

import app.cash.turbine.test
import com.aguiabranca.app.core.domain.ReportsRepository
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.DashboardState
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.FunnelData
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.network.NetworkHarness
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.ReportsApi
import com.aguiabranca.app.core.network.json
import com.aguiabranca.app.core.network.problemJson
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.feature.dashboard.data.RemoteReportsRepository
import com.aguiabranca.app.feature.dashboard.ui.DashboardViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import okhttp3.mockwebserver.MockResponse
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

fun sampleState(roi: Double? = 36.7, submitted: Int = 6) = DashboardState(
    funnel = FunnelData(submitted, 5, 3, 2, 1), roiConsolidated = roi, netProfit = 90000.0, totalInvestment = 245000.0,
    activeProjects = 1, avgProductivityGain = 8.25, totalCostReduction = 53000.0,
    sparklineRoi = listOf(null, null, null, null, null, 36.7f), guidelineImpacts = emptyList(), projectsByRoi = emptyList()
)

class FakeReportsRepository : ReportsRepository {
    val requested = mutableListOf<DashboardFilters>()
    var script: (DashboardFilters) -> Outcome<DashboardState> = { Outcome.Success(sampleState()) }
    val emissions = MutableSharedFlow<Unit>(extraBufferCapacity = 8)

    override fun observeSummary(filters: DashboardFilters): Flow<Outcome<DashboardState>> = flow {
        requested += filters
        emit(script(filters))
        emissions.collect { emit(script(filters)) }
    }

    data class InsightsCall(val filters: DashboardFilters, val guidelineId: String?, val refresh: Boolean)
    val insightsCalls = mutableListOf<InsightsCall>()
    var insightsScript: suspend (InsightsCall) -> Outcome<Insights> = { Outcome.Failure(DomainError.Unknown()) }

    override suspend fun generateInsights(filters: DashboardFilters, guidelineId: String?, refresh: Boolean): Outcome<Insights> {
        val call = InsightsCall(filters, guidelineId, refresh)
        insightsCalls += call
        return insightsScript(call)
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class DashboardViewModelTests {
    private val repo = FakeReportsRepository()

    @Before fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `renders exactly what the server sent, with no local calculation`() = runBlocking {
        repo.script = { Outcome.Success(sampleState(roi = null, submitted = 42)) }
        val vm = DashboardViewModel(repo)

        vm.state.test {
            val s = expectMostRecentItem()
            val data = (s as? UiState.Success)?.data ?: (awaitItem() as UiState.Success).data
            assertNull("ROI nulo do servidor vira '—' na tela", data.roiConsolidated)
            assertEquals(42, data.funnel.submitted)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `changing period or division asks the server again with the new filters`() = runBlocking {
        val vm = DashboardViewModel(repo)
        vm.state.test {
            skipItems(1)
            vm.setPeriod(Period.THIS_MONTH)
            vm.setDivision(Division.LOGISTICA)
            cancelAndIgnoreRemainingEvents()
        }
        assertEquals(
            listOf(DashboardFilters(), DashboardFilters(Period.THIS_MONTH), DashboardFilters(Period.THIS_MONTH, Division.LOGISTICA)),
            repo.requested
        )
    }

    @Test fun `first failure shows an error and retry fetches again`() = runBlocking {
        var fail = true
        repo.script = { if (fail) Outcome.Failure(DomainError.NetworkUnavailable()) else Outcome.Success(sampleState()) }
        val vm = DashboardViewModel(repo)

        vm.state.test {
            val error = expectMostRecentItem()
            assertTrue(error is UiState.Error && error.error is DomainError.NetworkUnavailable)

            fail = false
            vm.retry()

            assertTrue(expectMostRecentItem() is UiState.Success<*>)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `a transient failure after data is on screen keeps the last good summary`() = runBlocking {
        var fail = false
        repo.script = { if (fail) Outcome.Failure(DomainError.NetworkUnavailable()) else Outcome.Success(sampleState(submitted = 7)) }
        val vm = DashboardViewModel(repo)

        vm.state.test {
            val first = expectMostRecentItem()
            assertTrue(first is UiState.Success<*>)
            fail = true
            repo.emissions.emit(Unit)
            expectNoEvents()
            assertEquals(7, ((vm.state.value) as UiState.Success<DashboardState>).data.funnel.submitted)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `presentation mode toggles without touching the data`() {
        val vm = DashboardViewModel(repo)
        assertEquals(false, vm.presentation.value)
        vm.togglePresentation()
        assertEquals(true, vm.presentation.value)
    }
}

class RemoteReportsRepositoryTests {
    private val h = NetworkHarness()
    private val bus = RefreshBus()
    private val repo = RemoteReportsRepository(h.api(ReportsApi::class.java), bus)

    @After fun tearDown() = h.shutdown()

    private val summary = """{"period":"THIS_MONTH","division":"LOGISTICA","generatedAt":"2026-09-21T22:15:00Z",
        "funnel":{"submitted":6,"evaluated":5,"approved":3,"inExecution":2,"roiPositive":1},
        "kpis":{"roiConsolidated":36.73,"netProfit":90000,"totalInvestment":245000,"totalReturn":335000,"activeProjects":1,"avgProductivityGain":8.25,"totalCostReduction":53000,"overdueProjects":2},
        "sparkline":[{"month":"2026-04","roiPercent":null},{"month":"2026-05","roiPercent":-100},{"month":"2026-09","roiPercent":36.73}],
        "guidelineImpacts":[{"guidelineId":"g1","title":"Eficiência","ideasCount":3,"projectsCount":2,"investment":120000,"financialReturn":310000,"netProfit":190000,"roiPercent":158.33},
                            {"guidelineId":"g2","title":"Cliente","ideasCount":1,"projectsCount":0,"investment":0,"financialReturn":0,"netProfit":0,"roiPercent":null}],
        "projects":[{"id":"p1","title":"PROJ: A","stage":"CONCLUIDO","division":"LOGISTICA","guidelineId":"g1","guidelineTitle":"Eficiência","investment":120000,"financialReturn":310000,"netProfit":190000,"roiPercent":158.33,"productivityGain":12.5,"costReduction":45000,"targetDate":null,"daysToDeadline":null,"overdue":false,"statusText":"Entregue","updatedAt":"2026-09-10T12:00:00Z"},
                    {"id":"p2","title":"Rascunho","stage":"PLANEJAMENTO","division":"LOGISTICA","investment":0,"financialReturn":0,"netProfit":0,"roiPercent":null,"productivityGain":0,"costReduction":0,"overdue":false,"statusText":"","updatedAt":"2026-09-11T12:00:00Z"}]}"""

    @Test fun `summary maps funnel, KPIs, nullable sparkline, impacts and projects by ROI and sends the filters`() = runBlocking {
        h.server.enqueue(MockResponse().json(summary))

        val state = (repo.observeSummary(DashboardFilters(Period.THIS_MONTH, Division.LOGISTICA)).first() as Outcome.Success).value

        assertEquals(FunnelData(6, 5, 3, 2, 1), state.funnel)
        assertEquals(36.73, state.roiConsolidated!!, 0.0)
        assertEquals(listOf(null, -100f, 36.73f), state.sparklineRoi)
        assertEquals(listOf("2026-04", "2026-05", "2026-09"), state.sparklineMonths)
        assertEquals(2, state.overdueProjects)
        assertEquals(335000.0, state.totalReturn, 0.0)
        assertEquals(listOf(158.33, null), state.guidelineImpacts.map { it.roiPercent })
        assertEquals(listOf("p1" to 158.33, "p2" to null), state.projectsByRoi.map { it.first.id to it.second })
        assertEquals("Eficiência", state.projectsByRoi[0].first.guidelineTitle)
        assertEquals("/api/v1/reports/summary?period=THIS_MONTH&division=LOGISTICA", h.server.takeRequest().path)
    }

    @Test fun `without a division the query has only the period`() = runBlocking {
        h.server.enqueue(MockResponse().json("""{"funnel":{},"kpis":{},"sparkline":[],"guidelineImpacts":[],"projects":[]}"""))
        repo.observeSummary(DashboardFilters()).first()
        assertEquals("/api/v1/reports/summary?period=ALL", h.server.takeRequest().path)
    }

    @Test fun `editing a project invalidates reports and the dashboard refetches immediately`() = runBlocking {
        h.server.enqueue(MockResponse().json(summary))
        h.server.enqueue(MockResponse().json(summary.replace("\"submitted\":6", "\"submitted\":7")))

        repo.observeSummary(DashboardFilters()).test {
            assertEquals(6, (awaitItem() as Outcome.Success).value.funnel.submitted)
            bus.invalidate(RefreshKeys.REPORTS)
            assertEquals(7, (awaitItem() as Outcome.Success).value.funnel.submitted)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test fun `non leaders get PermissionDenied as a value, not a crash`() = runBlocking {
        h.server.enqueue(MockResponse().json(problemJson(403, "FORBIDDEN", "Você não tem permissão para esta ação."), 403))

        val outcome = repo.observeSummary(DashboardFilters()).first()

        assertEquals(DomainError.PermissionDenied("Você não tem permissão para esta ação."), (outcome as Outcome.Failure).error)
    }
}
