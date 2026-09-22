#include <jni.h>
#include "chromaprint.h"
extern "C" JNIEXPORT jlong JNICALL Java_io_hkmario_omni_AudioFingerprint_create(JNIEnv*, jobject, jint rate, jint channels) {
 auto *ctx=chromaprint_new(CHROMAPRINT_ALGORITHM_DEFAULT);
 if(!ctx || !chromaprint_start(ctx,rate,channels)){if(ctx)chromaprint_free(ctx);return 0;}
 return reinterpret_cast<jlong>(ctx);
}
extern "C" JNIEXPORT jboolean JNICALL Java_io_hkmario_omni_AudioFingerprint_feed(JNIEnv* env,jobject,jlong ptr,jshortArray samples) {
 auto *ctx=reinterpret_cast<ChromaprintContext*>(ptr);if(!ctx)return false;
 auto *data=env->GetShortArrayElements(samples,nullptr);if(!data)return false;
 auto ok=chromaprint_feed(ctx,data,env->GetArrayLength(samples));env->ReleaseShortArrayElements(samples,data,JNI_ABORT);return ok;
}
extern "C" JNIEXPORT jstring JNICALL Java_io_hkmario_omni_AudioFingerprint_finish(JNIEnv* env,jobject,jlong ptr) {
 auto *ctx=reinterpret_cast<ChromaprintContext*>(ptr);char *value=nullptr;
 if(!ctx||!chromaprint_finish(ctx)||!chromaprint_get_fingerprint(ctx,&value))return nullptr;
 auto out=env->NewStringUTF(value);chromaprint_dealloc(value);return out;
}
extern "C" JNIEXPORT void JNICALL Java_io_hkmario_omni_AudioFingerprint_free(JNIEnv*,jobject,jlong ptr){if(ptr)chromaprint_free(reinterpret_cast<ChromaprintContext*>(ptr));}
