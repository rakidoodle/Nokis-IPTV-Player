plugins {
    id("com.android.application") version "8.9.3" apply false
    id("org.jetbrains.kotlin.android") version "2.1.20" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.1.20" apply false
    id("org.jetbrains.kotlin.plugin.serialization") version "2.1.20" apply false
}

// Keep build intermediates off removable ExFAT volumes (AppleDouble ._ files break AAPT).
allprojects {
    layout.buildDirectory.set(file("${System.getProperty("user.home")}/.cache/noki-iptv-build/${rootProject.projectDir.absolutePath.hashCode().toUInt()}/${project.name}"))
}
