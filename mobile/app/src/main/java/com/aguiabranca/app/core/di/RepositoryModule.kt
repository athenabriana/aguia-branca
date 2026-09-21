package com.aguiabranca.app.core.di

import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.feature.auth.data.FirestoreUsersRepository
import com.aguiabranca.app.feature.guidelines.data.FirestoreGuidelinesRepository
import com.aguiabranca.app.feature.ideas.data.FirestoreIdeasRepository
import com.aguiabranca.app.feature.projects.data.FirestoreProjectsRepository
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {
    @Binds @Singleton abstract fun bindUsers(impl: FirestoreUsersRepository): UsersRepository
    @Binds @Singleton abstract fun bindGuidelines(impl: FirestoreGuidelinesRepository): GuidelinesRepository
    @Binds @Singleton abstract fun bindIdeas(impl: FirestoreIdeasRepository): IdeasRepository
    @Binds @Singleton abstract fun bindProjects(impl: FirestoreProjectsRepository): ProjectsRepository
}
