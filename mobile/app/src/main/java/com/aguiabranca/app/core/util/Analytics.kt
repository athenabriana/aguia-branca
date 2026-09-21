package com.aguiabranca.app.core.util

import android.os.Bundle
import com.google.firebase.analytics.FirebaseAnalytics
import com.google.firebase.crashlytics.FirebaseCrashlytics
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class Analytics @Inject constructor(
    private val analytics: FirebaseAnalytics,
    private val crashlytics: FirebaseCrashlytics
) {
    fun logIdeaCreated(hasGuideline: Boolean) {
        analytics.logEvent("idea_created", Bundle().apply { putBoolean("has_guideline", hasGuideline) })
    }

    fun logIdeaApproved(projectId: String) {
        analytics.logEvent("idea_approved", Bundle().apply { putString("project_id", projectId) })
    }

    fun logProjectCompleted(projectId: String, hasOriginatingIdea: Boolean) {
        analytics.logEvent("project_completed", Bundle().apply {
            putString("project_id", projectId)
            putBoolean("has_idea", hasOriginatingIdea)
        })
    }

    fun logPresentationMode() {
        analytics.logEvent("dashboard_presentation_mode", Bundle())
    }

    fun recordException(t: Throwable) {
        crashlytics.recordException(t)
    }
}
