package com.aguiabranca.app.core.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticInfo
import com.aguiabranca.app.core.ui.theme.SemanticSuccess
import com.aguiabranca.app.core.ui.theme.SemanticWarning

data class FunnelStage(val label: String, val count: Int, val color: Color)

@Composable
fun FunnelChart(
    stages: List<FunnelStage>,
    modifier: Modifier = Modifier,
    animateOnAppear: Boolean = false
) {
    var played by remember { mutableStateOf(!animateOnAppear) }
    LaunchedEffect(animateOnAppear) {
        if (animateOnAppear) played = true
    }
    val maxCount = (stages.maxOfOrNull { it.count } ?: 1).coerceAtLeast(1)

    Column(modifier = modifier.fillMaxWidth()) {
        stages.forEach { stage ->
            val fraction by animateFloatAsState(
                targetValue = if (played) stage.count.toFloat() / maxCount else 0f,
                animationSpec = tween(durationMillis = 800, easing = LinearEasing),
                label = "funnel-${stage.label}"
            )
            Row(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                Text(stage.label, fontSize = 12.sp, modifier = Modifier.width(110.dp))
                Spacer(Modifier.width(8.dp))
                Canvas(modifier = Modifier.weight(1f).height(20.dp)) {
                    val barWidth = size.width * fraction
                    drawRect(color = stage.color, size = androidx.compose.ui.geometry.Size(barWidth, size.height))
                }
                Spacer(Modifier.width(8.dp))
                Text("${stage.count}", fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
            }
        }
    }
}

@Composable
fun SparklineChart(
    values: List<Float?>,
    modifier: Modifier = Modifier,
    color: Color = MaterialTheme.colorScheme.primary
) {
    Canvas(modifier = modifier.fillMaxWidth().height(60.dp)) {
        if (values.isEmpty()) return@Canvas
        val pairs = values.mapIndexed { idx, v -> idx to v }.filter { it.second != null }
        if (pairs.size < 2) {
            pairs.forEach { (i, v) ->
                if (v != null) {
                    val x = size.width * i / (values.size - 1).coerceAtLeast(1).toFloat()
                    drawCircle(color = color, radius = 4f, center = Offset(x, size.height / 2))
                }
            }
            return@Canvas
        }
        val ys = pairs.mapNotNull { it.second }
        val minY = ys.min()
        val maxY = ys.max()
        val range = (maxY - minY).coerceAtLeast(0.0001f)
        val path = Path()
        var first = true
        pairs.forEach { (i, vv) ->
            val v = vv ?: return@forEach
            val x = size.width * i / (values.size - 1).coerceAtLeast(1).toFloat()
            val y = size.height - (v - minY) / range * size.height
            if (first) { path.moveTo(x, y); first = false } else path.lineTo(x, y)
            drawCircle(color = color, radius = 3f, center = Offset(x, y))
        }
        drawPath(path = path, color = color, style = Stroke(width = 3f))
    }
}

@Composable
fun KpiCard(label: String, value: String, modifier: Modifier = Modifier) {
    androidx.compose.material3.Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface,
        shape = androidx.compose.foundation.shape.RoundedCornerShape(16.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.size(4.dp))
            Text(value, fontWeight = FontWeight.Bold, fontSize = 22.sp)
        }
    }
}

@Composable
fun KpiCardAnimated(
    label: String,
    targetValue: Double?,
    format: (Double) -> String,
    modifier: Modifier = Modifier
) {
    if (targetValue == null) {
        KpiCard(label = label, value = "—", modifier = modifier)
        return
    }
    var playKey by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { playKey = true }
    val v by animateFloatAsState(
        targetValue = if (playKey) targetValue.toFloat() else 0f,
        animationSpec = tween(durationMillis = 1500, easing = LinearEasing),
        label = "kpi-$label"
    )
    KpiCard(label = label, value = format(v.toDouble()), modifier = modifier)
}

@Composable
fun statusColor(stage: String): Color = when (stage) {
    "PLANEJAMENTO" -> SemanticInfo
    "EM_EXECUCAO" -> SemanticWarning
    "CONCLUIDO" -> SemanticSuccess
    "CANCELADO" -> SemanticDanger
    else -> MaterialTheme.colorScheme.primary
}
