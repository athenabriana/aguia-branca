package com.aguiabranca.app.feature.dashboard.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.PlayArrow
import androidx.compose.material3.AssistChip
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.ui.components.FunnelChart
import com.aguiabranca.app.core.ui.components.FunnelStage
import com.aguiabranca.app.core.ui.components.GuidelineImpactCard
import com.aguiabranca.app.core.ui.components.KpiCard
import com.aguiabranca.app.core.ui.components.KpiCardAnimated
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.components.RoleScaffold
import com.aguiabranca.app.core.ui.components.SparklineChart
import com.aguiabranca.app.core.ui.components.StageBadge
import com.aguiabranca.app.core.ui.components.formatCurrency
import com.aguiabranca.app.core.ui.components.formatPercent
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticInfo
import com.aguiabranca.app.core.ui.theme.SemanticSuccess
import com.aguiabranca.app.core.ui.theme.SemanticWarning
import com.aguiabranca.app.core.util.Analytics
import com.aguiabranca.app.feature.ideas.ui.divisionLabel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    onOpenGuideline: (String) -> Unit,
    onOpenProject: (String) -> Unit,
    onTab: (NavTab) -> Unit,
    vm: DashboardViewModel = hiltViewModel(),
    analytics: Analytics? = null
) {
    val session = LocalSession.current ?: return
    val state by vm.state.collectAsState()
    val filters by vm.filters.collectAsState()
    val isPresenting by vm.presentation.collectAsState()

    if (isPresenting) {
        PresentationMode(state = state, onExit = { vm.togglePresentation() })
        return
    }

    RoleScaffold(
        role = session.role,
        currentTab = NavTab.DASHBOARD,
        onTabClick = onTab,
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Dashboard") },
                actions = {
                    IconButton(onClick = {
                        analytics?.logPresentationMode()
                        vm.togglePresentation()
                    }) {
                        Icon(Icons.Outlined.PlayArrow, contentDescription = "Apresentar")
                    }
                }
            )
        }
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).verticalScroll(rememberScrollState())) {
            FiltersBar(
                period = filters.period, onPeriod = vm::setPeriod,
                division = filters.division, onDivision = vm::setDivision
            )
            when (val s = state) {
                is UiState.Success -> DashboardBody(
                    s.data,
                    onOpenGuideline = onOpenGuideline,
                    onOpenProject = onOpenProject
                )
                else -> Box(Modifier.fillMaxWidth().height(200.dp), contentAlignment = Alignment.Center) { Text("Carregando…") }
            }
            Spacer(Modifier.height(24.dp))
        }
    }
}

@Composable
private fun FiltersBar(
    period: Period,
    onPeriod: (Period) -> Unit,
    division: Division?,
    onDivision: (Division?) -> Unit
) {
    Column(modifier = Modifier.fillMaxWidth().padding(16.dp)) {
        Text("Período", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Period.entries.forEach { p ->
                FilterChip(
                    selected = period == p,
                    onClick = { onPeriod(p) },
                    label = {
                        Text(when (p) {
                            Period.THIS_MONTH -> "Este mês"
                            Period.LAST_QUARTER -> "Último trimestre"
                            Period.THIS_YEAR -> "Este ano"
                            Period.ALL -> "Tudo"
                        })
                    }
                )
            }
        }
        Spacer(Modifier.height(8.dp))
        Text("Divisão", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            FilterChip(selected = division == null, onClick = { onDivision(null) }, label = { Text("Tudo") })
            Division.entries.forEach { d ->
                FilterChip(selected = division == d, onClick = { onDivision(d) }, label = { Text(divisionLabel(d)) })
            }
        }
    }
}

@Composable
private fun DashboardBody(
    state: com.aguiabranca.app.feature.dashboard.DashboardState,
    onOpenGuideline: (String) -> Unit,
    onOpenProject: (String) -> Unit
) {
    Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp)) {
        Surface(shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxWidth()) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text("Funil de inovação", fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(12.dp))
                val f = state.funnel
                FunnelChart(
                    stages = listOf(
                        FunnelStage("Submetidas", f.submitted, SemanticInfo),
                        FunnelStage("Avaliadas", f.evaluated, SemanticWarning),
                        FunnelStage("Aprovadas", f.approved, SemanticSuccess),
                        FunnelStage("Em execução", f.inExecution, MaterialTheme.colorScheme.primary),
                        FunnelStage("ROI positivo", f.roiPositive, SemanticDanger)
                    )
                )
            }
        }
        Spacer(Modifier.height(16.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            KpiCard("ROI consolidado", state.roiConsolidated?.let { formatPercent(it) } ?: "—", modifier = Modifier.weight(1f))
            KpiCard("Lucro líquido", formatCurrency(state.netProfit), modifier = Modifier.weight(1f))
        }
        Spacer(Modifier.height(8.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            KpiCard("Investimento total", formatCurrency(state.totalInvestment), modifier = Modifier.weight(1f))
            KpiCard("Projetos ativos", state.activeProjects.toString(), modifier = Modifier.weight(1f))
        }
        Spacer(Modifier.height(8.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            KpiCard("Ganho produtividade méd.", formatPercent(state.avgProductivityGain), modifier = Modifier.weight(1f))
            KpiCard("Redução de custo", formatCurrency(state.totalCostReduction), modifier = Modifier.weight(1f))
        }

        Spacer(Modifier.height(16.dp))
        Surface(shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxWidth()) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text("Tendência de ROI (últimos 6 meses)", fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(8.dp))
                SparklineChart(values = state.sparklineRoi)
            }
        }

        Spacer(Modifier.height(16.dp))
        Text("Impacto por orientação", fontWeight = FontWeight.SemiBold)
        Spacer(Modifier.height(8.dp))
        state.guidelineImpacts.forEach { impact ->
            GuidelineImpactCard(
                title = impact.title,
                ideasCount = impact.ideasCount,
                projectsCount = impact.projectsCount,
                roiPercent = impact.roiPercent,
                onClick = { onOpenGuideline(impact.guidelineId) },
                modifier = Modifier.padding(vertical = 4.dp)
            )
        }

        Spacer(Modifier.height(16.dp))
        Text("Projetos por ROI", fontWeight = FontWeight.SemiBold)
        Spacer(Modifier.height(8.dp))
        state.projectsByRoi.forEach { (project, roi) ->
            Surface(
                shape = RoundedCornerShape(14.dp),
                color = MaterialTheme.colorScheme.surface,
                modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).clickable { onOpenProject(project.id) }
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(project.title, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                        StageBadge(project.stage)
                    }
                    Spacer(Modifier.height(4.dp))
                    Text("ROI: ${roi?.let { formatPercent(it) } ?: "—"}", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun PresentationMode(
    state: UiState<com.aguiabranca.app.feature.dashboard.DashboardState>,
    onExit: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxSize().clickable(onClick = onExit),
        color = MaterialTheme.colorScheme.primary
    ) {
        Column(
            modifier = Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text("ÁGUIA BRANCA · Dashboard", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 24.sp)
            Spacer(Modifier.height(24.dp))
            when (val s = state) {
                is UiState.Success -> {
                    KpiCardAnimated("ROI consolidado", s.data.roiConsolidated, ::formatPercent, modifier = Modifier.fillMaxWidth())
                    Spacer(Modifier.height(8.dp))
                    KpiCardAnimated("Lucro líquido", s.data.netProfit, ::formatCurrency, modifier = Modifier.fillMaxWidth())
                    Spacer(Modifier.height(8.dp))
                    KpiCardAnimated("Investimento total", s.data.totalInvestment, ::formatCurrency, modifier = Modifier.fillMaxWidth())
                    Spacer(Modifier.height(16.dp))
                    val f = s.data.funnel
                    FunnelChart(
                        animateOnAppear = true,
                        stages = listOf(
                            FunnelStage("Submetidas", f.submitted, SemanticInfo),
                            FunnelStage("Avaliadas", f.evaluated, SemanticWarning),
                            FunnelStage("Aprovadas", f.approved, SemanticSuccess),
                            FunnelStage("Em execução", f.inExecution, Color.White),
                            FunnelStage("ROI positivo", f.roiPositive, SemanticDanger)
                        )
                    )
                }
                else -> Text("Carregando…", color = Color.White)
            }
            Spacer(Modifier.height(24.dp))
            Text("Toque para sair", color = Color.White.copy(alpha = 0.8f), fontSize = 12.sp)
        }
    }
}
