package io.hkmario.omni
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.*
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.drawscope.*
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp

@Composable fun FeatureIcon(name:String,modifier:Modifier=Modifier.size(23.dp)) {
 val ink=if(name=="complete")Color(0xFF29C978) else LocalContentColor.current
 val data=when(name){
  "complete"->"M12,2 A10,10 0 1 1 11.99,2 M6,12 L10,16 L18,8"
  "video"->"M3,4 L21,4 L21,20 L3,20 Z M7,4 L7,20 M17,4 L17,20 M3,9 L7,9 M17,9 L21,9"
  "audio"->"M9,18 L9,5 L21,2 L21,15 M9,8 L21,5 M9,18 C9,22 2,22 2,19 C2,16 9,15 9,18 M21,15 C21,19 14,19 14,16 C14,13 21,12 21,15"
  "delete"->"M3,6 L21,6 M8,6 L8,3 L16,3 L16,6 M5,6 L6,22 L18,22 L19,6 M10,10 L10,18 M14,10 L14,18"
  "size"->"M4,3 L20,3 L22,10 L22,21 L2,21 L2,10 Z M2,10 L22,10 M16,16 L19,16"
  "folder"->"M2,6 L9,6 L11,9 L22,9 L22,21 L2,21 Z"
  "search"->"M10,2 A8,8 0 1 1 9.99,2 M16,16 L23,23"
  else->"M12,3 L12,21 M3,12 L21,12"
 }
 Canvas(modifier){scale(size.width/24f,size.height/24f,androidx.compose.ui.geometry.Offset.Zero){drawPath(PathParser().parsePathString(data).toPath(),ink,style=Stroke(1.8f,cap=StrokeCap.Round))}}
}
@Composable fun AppCheckbox(checked:Boolean,onCheckedChange:((Boolean)->Unit)?,modifier:Modifier=Modifier,enabled:Boolean=true){
 val outline=MaterialTheme.colorScheme.onSurfaceVariant;val background=MaterialTheme.colorScheme.surface
 Box(modifier.sizeIn(minWidth=48.dp,minHeight=48.dp).then(if(onCheckedChange!=null)Modifier.toggleable(checked,enabled,Role.Checkbox){onCheckedChange(it)}else Modifier),contentAlignment=Alignment.Center){
  Box(Modifier.size(24.dp).background(if(checked)Brush.linearGradient(listOf(Color(0xFF6130FF),Color(0xFF168FFF)))else Brush.linearGradient(listOf(background,background)),RoundedCornerShape(5.dp)).border(1.dp,if(checked)Color(0xFF278FFF)else outline,RoundedCornerShape(5.dp))){if(checked)Canvas(Modifier.fillMaxSize().padding(5.dp)){val p=Path().apply{moveTo(0f,size.height*.5f);lineTo(size.width*.35f,size.height*.85f);lineTo(size.width,size.height*.1f)};drawPath(p,Color.White,style=Stroke(2.dp.toPx(),cap=StrokeCap.Round))}}
 }
}
