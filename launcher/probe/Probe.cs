using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
public static class Probe {
 [DllImport("libcapi-appfw-app-operation-tv.so.0")] static extern int app_operation_get_current_app(string operation,out IntPtr app);
 [DllImport("libcapi-appfw-app-operation-tv.so.0")] static extern int app_operation_set_current_app(string operation,string app);
 [DllImport("libsource-api.so")] static extern IntPtr get_source_list();
 [DllImport("libvconf.so.0")] static extern int vconf_get_int(string key,out int value);
 [DllImport("libc.so.6")] static extern void free(IntPtr p);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_volume(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_mute(out int value);
 [DllImport("libcapi-ui-efl-util.so.0")] static extern IntPtr efl_util_screenshot_initialize(int width,int height);
 [DllImport("libcapi-ui-efl-util.so.0")] static extern IntPtr efl_util_screenshot_take_tbm_surface(IntPtr screenshot);
 [DllImport("libcapi-ui-efl-util.so.0")] static extern int efl_util_screenshot_deinitialize(IntPtr screenshot);
 [DllImport("libtbm.so.1")] static extern int tbm_surface_map(IntPtr surface,int opt,IntPtr info);
 [DllImport("libtbm.so.1")] static extern int tbm_surface_unmap(IntPtr surface);
 [DllImport("libtbm.so.1")] static extern void tbm_surface_destroy(IntPtr surface);
 // Capture the UI layer (the video plane comes out black) as an RGB PNG small enough for tv pull.
 static int Screenshot(string path,int width,int height) {
  IntPtr shot=efl_util_screenshot_initialize(width,height);if(shot==IntPtr.Zero){Console.WriteLine("screenshot=init_failed");return 1;}
  IntPtr surface=efl_util_screenshot_take_tbm_surface(shot),info=Marshal.AllocHGlobal(256);
  try {
   if(surface==IntPtr.Zero){Console.WriteLine("screenshot=take_failed");return 1;}
   for(int i=0;i<256;i++)Marshal.WriteByte(info,i,0);
   int r=tbm_surface_map(surface,1,info);if(r!=0){Console.WriteLine("screenshot=map_failed:"+r);return 1;}
   // tbm_surface_info_s: width, height, format, bpp, size, num_planes, then planes {ptr, size, offset, stride}; XRGB8888 bytes are B,G,R,X.
   int w=Marshal.ReadInt32(info,0),h=Marshal.ReadInt32(info,4),stride=Marshal.ReadInt32(info,36);IntPtr pixels=Marshal.ReadIntPtr(info,24);
   var row=new byte[stride];var line=new byte[1+w*3];var data=new MemoryStream();
   using(var z=new ZLibStream(data,CompressionLevel.Optimal,true))
    for(int y=0;y<h;y++){Marshal.Copy(pixels+y*stride,row,0,stride);for(int x=0;x<w;x++){line[1+x*3]=row[x*4+2];line[2+x*3]=row[x*4+1];line[3+x*3]=row[x*4];}z.Write(line);}
   tbm_surface_unmap(surface);
   var header=new byte[13];BigEndian(w).CopyTo(header,0);BigEndian(h).CopyTo(header,4);header[8]=8;header[9]=2;
   using(var o=File.Create(path)){o.Write(new byte[]{137,80,78,71,13,10,26,10});Chunk(o,"IHDR",header);Chunk(o,"IDAT",data.ToArray());Chunk(o,"IEND",new byte[0]);Console.WriteLine("screenshot="+w+"x"+h+" bytes="+o.Length);}
   return 0;
  } finally {Marshal.FreeHGlobal(info);if(surface!=IntPtr.Zero)tbm_surface_destroy(surface);efl_util_screenshot_deinitialize(shot);}
 }
 static byte[] BigEndian(int v){var b=BitConverter.GetBytes(v);if(BitConverter.IsLittleEndian)Array.Reverse(b);return b;}
 static void Chunk(Stream o,string type,byte[] data) {
  var body=new byte[4+data.Length];for(int i=0;i<4;i++)body[i]=(byte)type[i];data.CopyTo(body,4);
  uint crc=0xFFFFFFFF;foreach(var b in body){crc^=b;for(int k=0;k<8;k++)crc=(crc&1)!=0?0xEDB88320^(crc>>1):crc>>1;}
  o.Write(BigEndian(data.Length));o.Write(body);o.Write(BigEndian((int)(crc^0xFFFFFFFF)));
 }
 public static int Main(string[] args) {
  try {
   if(args.Length==2 && args[0]=="set-home") {int s=app_operation_set_current_app("http://tizen.org/appcontrol/operation/main",args[1]); Console.WriteLine("set_home="+s);return s==0?0:1;}
   if((args.Length==2||args.Length==4) && args[0]=="screenshot") return Screenshot(args[1],args.Length==4?int.Parse(args[2]):1920,args.Length==4?int.Parse(args[3]):1080);
   if(args.Length==1 && args[0]=="state") {int v,m;int vr=sound_manager_get_master_volume(out v),mr=sound_manager_get_master_mute(out m);Console.WriteLine("volume="+v+" volume_result="+vr+" mute="+m+" mute_result="+mr);return vr==0&&mr==0?0:1;}
   IntPtr p;int r=app_operation_get_current_app("http://tizen.org/appcontrol/operation/main",out p);
   Console.WriteLine("home_result="+r+" home="+(p==IntPtr.Zero?"null":Marshal.PtrToStringAnsi(p)));if(p!=IntPtr.Zero)free(p);
   p=get_source_list();Console.WriteLine("sources="+(p==IntPtr.Zero?"null":Marshal.PtrToStringAnsi(p)));
   int source; r=vconf_get_int("memory/system/source_type",out source);Console.WriteLine("current_source_result="+r+" current_source="+source);
   return 0;
  } catch(Exception e) {Console.WriteLine(e);return 1;}
 }
}
