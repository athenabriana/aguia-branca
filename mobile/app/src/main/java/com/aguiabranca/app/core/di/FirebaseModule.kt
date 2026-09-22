package com.aguiabranca.app.core.di

import com.google.firebase.Firebase
import com.google.firebase.analytics.FirebaseAnalytics
import com.google.firebase.analytics.analytics
import com.google.firebase.crashlytics.FirebaseCrashlytics
import com.google.firebase.crashlytics.crashlytics
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/** Só telemetria (Analytics e Crashlytics). Autenticação e dados vêm da API própria — o Firebase Auth/Firestore foram removidos. */
@Module
@InstallIn(SingletonComponent::class)
object FirebaseModule {
    @Provides @Singleton fun provideAnalytics(): FirebaseAnalytics = Firebase.analytics
    @Provides @Singleton fun provideCrashlytics(): FirebaseCrashlytics = Firebase.crashlytics
}
