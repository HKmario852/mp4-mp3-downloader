package io.hkmario.omni
import android.app.Application
class OmniApp : Application() {
    lateinit var engine: Engine
    override fun onCreate() { super.onCreate(); engine = Engine(this); Notices.create(this) }
}
