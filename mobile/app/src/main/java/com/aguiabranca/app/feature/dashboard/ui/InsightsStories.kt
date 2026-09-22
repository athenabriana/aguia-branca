package com.aguiabranca.app.feature.dashboard.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.model.InsightPriority
import com.aguiabranca.app.core.domain.model.InsightRecommendation
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.ui.components.Pill
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticInfo
import com.aguiabranca.app.core.ui.theme.SemanticWarning
import kotlinx.coroutines.delay

private const val StoryDurationMs = 6_000L
private const val StoryTickMs = 50L

/** Uma página do reel: resumo, um bloco de itens (destaques/riscos) ou uma recomendação. */
private sealed interface StoryPage {
    data class SummaryPage(val text: String, val generatedAt: Long, val fromCache: Boolean, val model: String) : StoryPage
    data class BulletsPage(val title: String, val icon: String, val items: List<String>, val accent: Color) : StoryPage
    data class RecommendationPage(val item: InsightRecommendation, val guidelineTitle: String?) : StoryPage
}

/** Resumo sempre existe; destaques/riscos só entram se houver algo; uma recomendação = uma página. */
private fun buildStoryPages(insights: Insights, guidelineTitles: Map<String, String>): List<StoryPage> = buildList {
    add(StoryPage.SummaryPage(insights.summary, insights.generatedAt, insights.fromCache, insights.model))
    if (insights.highlights.isNotEmpty()) add(StoryPage.BulletsPage("Destaques", "✨", insights.highlights, SemanticInfo))
    if (insights.risks.isNotEmpty()) add(StoryPage.BulletsPage("Riscos", "⚠️", insights.risks, SemanticDanger))
    insights.recommendations.forEach { add(StoryPage.RecommendationPage(it, it.relatedGuidelineId?.let(guidelineTitles::get))) }
}

/**
 * Insights em formato de stories (Instagram): uma página por seção, com barra de progresso segmentada, avanço
 * automático por tempo, toque nas laterais para navegar e toque-e-segure para pausar (útil nas páginas com mais
 * texto). Passar da última página, ou voltar a partir da primeira, chama [onFinished] (volta à tela inicial da
 * apresentação, de onde dá para gerar um novo); o ✕ sempre sai da apresentação inteira.
 */
@Composable
fun InsightsStoriesView(
    insights: Insights,
    guidelineTitles: Map<String, String>,
    onRegenerate: () -> Unit,
    onFinished: () -> Unit,
    onClose: () -> Unit
) {
    val pages = remember(insights) { buildStoryPages(insights, guidelineTitles) }
    var pageIndex by rememberSaveable(insights) { mutableStateOf(0) }
    var progress by remember(pageIndex) { mutableStateOf(0f) }
    var paused by remember { mutableStateOf(false) }
    val onFinishedState = rememberUpdatedState(onFinished)

    fun goNext() { if (pageIndex < pages.lastIndex) pageIndex++ else onFinishedState.value() }
    fun goPrev() { if (pageIndex > 0) pageIndex-- else onFinishedState.value() }

    // Avanço automático por tempo, pausável (toque-e-segure); reinicia a cada página.
    LaunchedEffect(pageIndex, pages) {
        var elapsed = 0L
        progress = 0f
        while (elapsed < StoryDurationMs) {
            delay(StoryTickMs)
            if (!paused) {
                elapsed += StoryTickMs
                progress = (elapsed.toFloat() / StoryDurationMs).coerceIn(0f, 1f)
            }
        }
        goNext()
    }

    val page = pages.getOrNull(pageIndex) ?: return
    val background = when (page) {
        is StoryPage.SummaryPage -> MaterialTheme.colorScheme.primary
        is StoryPage.BulletsPage -> page.accent
        is StoryPage.RecommendationPage -> SemanticWarning
    }

    Surface(modifier = Modifier.fillMaxSize(), color = background) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .pointerInput(pageIndex) {
                    detectTapGestures(
                        onPress = {
                            paused = true
                            tryAwaitRelease()
                            paused = false
                        },
                        onTap = { offset -> if (offset.x < size.width / 2) goPrev() else goNext() }
                    )
                }
        ) {
            Column(modifier = Modifier.fillMaxSize().padding(20.dp)) {
                StoryProgressBar(count = pages.size, current = pageIndex, progress = progress)
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("✨ Insights da IA", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 14.sp, modifier = Modifier.weight(1f))
                    IconButton(onClick = onRegenerate) { Icon(Icons.Outlined.Refresh, contentDescription = "Gerar novo", tint = Color.White) }
                    IconButton(onClick = onClose) { Icon(Icons.Outlined.Close, contentDescription = "Fechar", tint = Color.White) }
                }
                Box(modifier = Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                    StoryPageContent(page)
                }
                Text(AI_DISCLAIMER, color = Color.White.copy(alpha = 0.75f), fontSize = 11.sp, modifier = Modifier.align(Alignment.CenterHorizontally))
            }
        }
    }
}

/** Barra de progresso segmentada (uma faixa por página): cheia = já vista, parcial = atual, vazia = por vir. */
@Composable
private fun StoryProgressBar(count: Int, current: Int, progress: Float, modifier: Modifier = Modifier) {
    Row(modifier = modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
        repeat(count) { i ->
            val fraction = when {
                i < current -> 1f
                i == current -> progress
                else -> 0f
            }
            Box(
                modifier = Modifier.weight(1f).height(3.dp).clip(RoundedCornerShape(2.dp)).background(Color.White.copy(alpha = 0.35f))
            ) {
                Box(modifier = Modifier.fillMaxHeight().fillMaxWidth(fraction).background(Color.White))
            }
        }
    }
}

@Composable
private fun StoryPageContent(page: StoryPage) {
    when (page) {
        is StoryPage.SummaryPage -> Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("📊 Resumo", color = Color.White.copy(alpha = 0.8f), fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
            Text(page.text, color = Color.White, fontSize = 20.sp, textAlign = TextAlign.Center)
            Spacer(Modifier.height(8.dp))
            Text(
                buildString {
                    append(formatGeneratedAt(page.generatedAt))
                    if (page.fromCache) append(" · em cache")
                },
                color = Color.White.copy(alpha = 0.7f), fontSize = 11.sp
            )
        }
        is StoryPage.BulletsPage -> Column(horizontalAlignment = Alignment.Start, verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Text("${page.icon} ${page.title}", color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Bold)
            page.items.forEach { Text("•  $it", color = Color.White, fontSize = 16.sp) }
        }
        is StoryPage.RecommendationPage -> Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("🎯 Recomendação", color = Color.White.copy(alpha = 0.8f), fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
            PriorityChipOnDark(page.item.priority)
            Text(page.item.title, color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
            Text(page.item.detail, color = Color.White, fontSize = 15.sp, textAlign = TextAlign.Center)
            page.guidelineTitle?.let { Text("Orientação: $it", color = Color.White.copy(alpha = 0.85f), fontSize = 12.sp) }
        }
    }
}

@Composable
private fun PriorityChipOnDark(priority: InsightPriority) {
    val label = when (priority) {
        InsightPriority.ALTA -> "ALTA"
        InsightPriority.MEDIA -> "MÉDIA"
        InsightPriority.BAIXA -> "BAIXA"
    }
    Pill(label, bg = Color.White.copy(alpha = 0.22f), fg = Color.White)
}
