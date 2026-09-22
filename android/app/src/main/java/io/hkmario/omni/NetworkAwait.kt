package io.hkmario.omni
import kotlinx.coroutines.suspendCancellableCoroutine
import okhttp3.Call
import okhttp3.Callback
import okhttp3.Response
import java.io.IOException
import kotlin.coroutines.resumeWithException

// Cancelling a review aborts HTTP while headers are pending, instead of retaining a blocking execute().
internal suspend fun Call.awaitResponse():Response=suspendCancellableCoroutine{continuation->
 continuation.invokeOnCancellation{cancel()}
 enqueue(object:Callback{
  override fun onFailure(call:Call,e:IOException){if(continuation.isActive)continuation.resumeWithException(e)}
  override fun onResponse(call:Call,response:Response){continuation.resume(response,onCancellation={_,value,_->value.close()})}
 })
}
