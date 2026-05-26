package com.aguiabranca.app

import android.app.Application
import com.google.firebase.Firebase
import com.google.firebase.analytics.analytics
import com.google.firebase.crashlytics.crashlytics
import dagger.hilt.android.HiltAndroidApp

@HiltAndroidApp
class AguiaBrancaApplication : Application() {

    override fun onCreate() {
        super.onCreate()
        val enableTelemetry = !BuildConfig.DEBUG
        Firebase.crashlytics.isCrashlyticsCollectionEnabled = enableTelemetry
        Firebase.analytics.setAnalyticsCollectionEnabled(enableTelemetry)
    }
}
