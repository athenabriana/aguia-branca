-keepattributes Signature, InnerClasses, EnclosingMethod
-keepattributes RuntimeVisibleAnnotations, RuntimeVisibleParameterAnnotations
-keepattributes AnnotationDefault

-keepclassmembers,allowobfuscation class * {
  @kotlinx.serialization.SerialName <fields>;
}
-keep,includedescriptorclasses class com.aguiabranca.app.**$$serializer { *; }
-keepclassmembers class com.aguiabranca.app.** { *** Companion; }
-keepclasseswithmembers class com.aguiabranca.app.** { kotlinx.serialization.KSerializer serializer(...); }
