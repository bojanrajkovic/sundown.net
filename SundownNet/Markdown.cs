using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Sundown
{
	public sealed class Markdown : IDisposable
	{
		[DllImport("sundown", CallingConvention=CallingConvention.Cdecl)]
		internal static extern void sd_version(out int major, out int minor, out int revision);

		public static Version Version {
			get {
				int major, minor, revision;
				sd_version(out major, out minor, out revision);
				return new Version(major, minor, revision);
			}
		}

		readonly Buffer buffer = Buffer.Create();

		IntPtr ptr;
		Renderer renderer;
		readonly MarkdownExtensions extensions;

		static readonly Regex relaxedLineEndingsMatcher = new Regex (@"^[\w\<][^\n]*\n+", RegexOptions.Compiled);
		static readonly Regex twoNewlines = new Regex ("\n{2}", RegexOptions.Compiled);

		[DllImport("sundown", CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr sd_markdown_new(uint extensions, IntPtr max_nesting, IntPtr callbacks, IntPtr opaque);

		[DllImport ("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern IntPtr LoadLibrary (string lpFileName);

		[DllImport ("/usr/lib/libSystem.dylib")]
		public static extern IntPtr dlopen (string file, int mode);

		public static void Init ()
		{
			var tempDirectory = Path.GetTempFileName();
			File.Delete(tempDirectory);
			Directory.CreateDirectory(tempDirectory);

			IntPtr handle;
			string dllPath;

			if (Path.DirectorySeparatorChar == '\\') {
				dllPath = Path.Combine(tempDirectory, "sundown.dll");
				if (IntPtr.Size == 8) {
					using (var fos = File.Create(dllPath)) {
						using (var ms = new MemoryStream (Properties.Resources.sundown64)) {
							ms.CopyTo(fos);
						}
					}
				} else {
					using (var fos = File.Create(dllPath)) {
						using (var ms = new MemoryStream (Properties.Resources.sundown)) {
							ms.CopyTo(fos);
						}
					}
				}

				handle = LoadLibrary(dllPath);
			} else {
				dllPath = Path.Combine(tempDirectory, "libsundown.dylib");
				using (var fos = File.Create(dllPath)) {
					using (var ms = new MemoryStream (Properties.Resources.libsundown)) {
						ms.CopyTo(fos);
					}
				}

				handle = dlopen(dllPath, 0);
			}

			Debug.Assert(handle != IntPtr.Zero, "Unable to load library " + dllPath);
		}

		public Markdown(Renderer renderer)
			: this(renderer, null)
		{
		}

		public Markdown(Renderer renderer, MarkdownExtensions extensions)
			: this(renderer, extensions, 16)
		{
		}

		public Markdown(Renderer renderer, int maxNesting)
			: this(renderer, null, maxNesting)
		{
		}

		unsafe public Markdown(Renderer renderer, MarkdownExtensions extensions, int maxNesting)
		{
			this.renderer = renderer;
			this.extensions = extensions;

			ptr = sd_markdown_new((extensions == null ? 0 : extensions.ToUInt()), (IntPtr)maxNesting,
				renderer.callbacksgchandle.AddrOfPinnedObject(), renderer.opaque);
		}

		~Markdown()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		[DllImport("sundown", CallingConvention=CallingConvention.Cdecl)]
		internal static extern void sd_markdown_free(IntPtr ptr);

		void Dispose(bool disposing)
		{
			if (disposing) {
				GC.SuppressFinalize(this);
			}

			if (ptr != IntPtr.Zero) {
				sd_markdown_free(ptr);
				ptr = IntPtr.Zero;
			}

			if (buffer != null) {
				buffer.Dispose();
			}

			renderer = null;
		}

		public void Render(Buffer @out, string str)
		{
			Render(@out, @out.Encoding, str);
		}

		public void Render(Buffer @out, Encoding encoding, string str)
		{
			Render(@out, encoding.GetBytes(str));
		}

		public void Render(Buffer @out, Buffer @in)
		{
			sd_markdown_render(@out.NativeHandle, @in.Data, @in.Size, ptr);
		}

		public void Render(Buffer @out, byte[] array)
		{
			Render(@out, array, array.LongLength);
		}

		public void Render(Buffer @out, byte[] array, int length)
		{
			Render(@out, array, (IntPtr)length);
		}

		public void Render(Buffer @out, byte[] array, long length)
		{
			Render(@out, array, (IntPtr)length);
		}

		[DllImport("sundown", CallingConvention=CallingConvention.Cdecl)]
		internal static extern void sd_markdown_render(IntPtr buf, IntPtr document, IntPtr documentSize, IntPtr md);

		public void Render(Buffer @out, byte[] array, IntPtr length)
		{
			var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
			sd_markdown_render(@out.NativeHandle, handle.AddrOfPinnedObject(), length, ptr);
			handle.Free();
		}

		public byte[] Transform(byte[] data)
		{
			buffer.Size = IntPtr.Zero;
			Render(buffer, data);
			return buffer.GetBytes();
		}

		public string Transform(string str)
		{
			if (extensions.RelaxLineEndings)
				str = relaxedLineEndingsMatcher.Replace (str, match => twoNewlines.IsMatch(match.ToString()) ? match.ToString() : match + "  \n");
			buffer.Size = IntPtr.Zero;
			Render(buffer, str);
			return buffer.ToString();
		}

		public Buffer Transform(Buffer @in)
		{
			buffer.Size = IntPtr.Zero;
			Render(buffer, @in);
			return buffer;
		}

		#region SmartyPants

		public static void SmartyPants(Buffer @out, string str)
		{
			SmartyPants(@out, Encoding.Default, str);
		}

		public static void SmartyPants(Buffer @out, Encoding encoding, string str)
		{
			SmartyPants(@out, encoding.GetBytes(str));
		}

		public static void SmartyPants(Buffer @out, byte[] array)
		{
			SmartyPants(@out, array, array.LongLength);
		}

		public static void SmartyPants(Buffer @out, byte[] array, int length)
		{
			SmartyPants(@out, array, (IntPtr)length);
		}

		public static void SmartyPants(Buffer @out, byte[] array, long length)
		{
			SmartyPants(@out, array, (IntPtr)length);
		}

		[DllImport("sundown", CallingConvention=CallingConvention.Cdecl)]
		internal static extern void sdhtml_smartypants(IntPtr buf, IntPtr text, IntPtr size);

		public static void SmartyPants(Buffer @out, byte[] array, IntPtr length)
		{
			var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
			sdhtml_smartypants(@out.NativeHandle, handle.AddrOfPinnedObject(), (IntPtr)length);
			handle.Free();
		}

		#endregion
	}
}

