-keepattributes Signature, InnerClasses, EnclosingMethod
-keepattributes RuntimeVisibleAnnotations, RuntimeVisibleParameterAnnotations
-keepattributes AnnotationDefault

# kotlinx-serialization
-keepclassmembers,allowobfuscation class * {
  @kotlinx.serialization.SerialName <fields>;
}
-keep,includedescriptorclasses class com.aguiabranca.app.**$$serializer { *; }
-keepclassmembers class com.aguiabranca.app.** { *** Companion; }
-keepclasseswithmembers class com.aguiabranca.app.** { kotlinx.serialization.KSerializer serializer(...); }

# Firestore DTOs precisam de no-arg constructor + getters/setters intactos
# para a desserialização reflexiva funcionar quando R8 estiver habilitado.
-keep class com.aguiabranca.app.core.data.dto.** { *; }
-keepclassmembers class com.aguiabranca.app.core.data.dto.** {
    <init>(...);
    public *;
}

# Enums de domínio são serializados como strings em Firestore — preservar nomes.
-keepclassmembers enum com.aguiabranca.app.core.domain.model.** { *; }
