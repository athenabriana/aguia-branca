package com.aguiabranca.app.core.di

import com.google.firebase.Firebase
import com.google.firebase.analytics.FirebaseAnalytics
import com.google.firebase.analytics.analytics
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.auth
import com.google.firebase.crashlytics.FirebaseCrashlytics
import com.google.firebase.crashlytics.crashlytics
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.firestore
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object FirebaseModule {
    @Provides @Singleton fun provideAuth(): FirebaseAuth = Firebase.auth
    @Provides @Singleton fun provideFirestore(): FirebaseFirestore = Firebase.firestore
    @Provides @Singleton fun provideAnalytics(): FirebaseAnalytics = Firebase.analytics
    @Provides @Singleton fun provideCrashlytics(): FirebaseCrashlytics = Firebase.crashlytics
}
