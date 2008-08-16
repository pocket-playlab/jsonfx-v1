#region License
/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2008 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/
#endregion License

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Web.Compilation;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text;

using BuildTools;
using JsonFx.Handlers;

namespace JsonFx.Compilation
{
	public class MergeResourceCodeProvider : JsonFx.Compilation.ResourceCodeProvider
	{
		#region Constants

		private static readonly char[] TypeDelims = { ',' };

		#endregion Constants

		#region Fields

		private string[] sources = null;
		private string contentType = "text/plain";
		private string fileExtension = "txt";
		private bool isMimeSet = false;

		#endregion Fields

		#region ResourceCodeProvider Members

		public override string FileExtension
		{
			get { return this.fileExtension; }
		}

		public override string ContentType
		{
			get { return this.contentType; }
		}

		protected internal override IList<ParseException> PreProcess(
			IResourceBuildHelper helper,
			string virtualPath,
			string sourceText,
			TextWriter writer)
		{
			if (String.IsNullOrEmpty(sourceText))
			{
				return null;
			}

			this.sources = sourceText.Replace("\r\n", "\n").Split('\n');

			List<ParseException> errors = new List<ParseException>();
			for (int i=0; i<this.sources.Length; i++)
			{
				try
				{
					if (String.IsNullOrEmpty(this.sources[i]))
					{
						continue;
					}

					if (this.sources[i].StartsWith("//") ||
						this.sources[i].StartsWith("#"))
					{
						this.sources[i] = null;
						continue;
					}

					if (this.sources[i].IndexOf(',') >= 0)
					{
						string preProcessed, compacted;
						this.ProcessEmbeddedResource(helper, this.sources[i], out preProcessed, out compacted, errors);

						writer.Write(preProcessed);
						this.sources[i] = compacted;
						continue;
					}

					if (this.sources[i].StartsWith("/"))
					{
						// ensure app-relative paths for BuildManager lookup
						this.sources[i] = "~"+this.sources[i];
					}

					CompiledBuildResult result = CompiledBuildResult.Create(this.sources[i]);
					if (result != null)
					{
						if (!this.isMimeSet &&
							!String.IsNullOrEmpty(result.ContentType) &&
							!String.IsNullOrEmpty(result.FileExtension))
						{
							this.contentType = result.ContentType;
							this.fileExtension = result.FileExtension;
							this.isMimeSet = true;
						}

						helper.AddVirtualPathDependency(this.sources[i]);

						writer.WriteLine(result.Resource);
						this.sources[i] = result.CompactedResource;
						continue;
					}

					string source = BuildManager.GetCompiledCustomString(this.sources[i]);
					if (!String.IsNullOrEmpty(source))
					{
						helper.AddVirtualPathDependency(this.sources[i]);

						writer.WriteLine(source);
						this.sources[i] = source;
						continue;
					}

					source = helper.OpenReader(this.sources[i]).ReadToEnd();
					if (!String.IsNullOrEmpty(source))
					{
						helper.AddVirtualPathDependency(this.sources[i]);

						writer.WriteLine(source);
						this.sources[i] = source;
						continue;
					}
				}
				catch (Exception ex)
				{
					errors.Add(new ParseError(ex.Message, virtualPath, i+1, 1, ex));
				}
			}

			return errors;
		}

		private void ProcessEmbeddedResource(
			IResourceBuildHelper helper,
			string source,
			out string preProcessed,
			out string compacted,
			List<ParseException> errors)
		{
			preProcessed = source.Replace(" ", "");
			string[] parts = preProcessed.Split(TypeDelims, 2, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length < 2 ||
				String.IsNullOrEmpty(parts[0]) ||
				String.IsNullOrEmpty(parts[1]))
			{
				compacted = preProcessed = null;
				return;
			}

			parts[0] = MergeResourceCodeProvider.ScrubResourceName(parts[0]);

			// load resources from Assembly
			Assembly assembly = Assembly.Load(parts[1]);
			helper.AddAssemblyDependency(assembly);

			ManifestResourceInfo info = assembly.GetManifestResourceInfo(parts[0]);
			if (info == null)
			{
				compacted = preProcessed = null;
				return;
			}

			using (Stream stream = assembly.GetManifestResourceStream(parts[0]))
			{
				using (StreamReader reader = new StreamReader(stream))
				{
					preProcessed = reader.ReadToEnd();
				}
			}

			string ext = Path.GetExtension(parts[0]).Trim('.');
			CompilerType compiler = helper.GetDefaultCompilerTypeForLanguage(ext);
			if (!typeof(ResourceCodeProvider).IsAssignableFrom(compiler.CodeDomProviderType))
			{
				// don't know how to process any further
				compacted = preProcessed;
				return;
			}

			ResourceCodeProvider provider = (ResourceCodeProvider)Activator.CreateInstance(compiler.CodeDomProviderType);
			using (StringWriter sw = new StringWriter())
			{
				IList<ParseException> parseErrors = provider.PreProcess(
					helper,
					parts[0],
					preProcessed,
					sw);
				if (parseErrors != null && parseErrors.Count > 0)
				{
					// report any errors
					errors.AddRange(parseErrors);
				}
				sw.Flush();

				// concatenate the preprocessed source for current merge phase
				preProcessed = sw.ToString();
			}

			using (StringWriter sw = new StringWriter())
			{
				IList<ParseException> parseErrors = provider.Compact(
					helper,
					parts[0],
					preProcessed,
					sw);
				if (parseErrors != null && parseErrors.Count > 0)
				{
					// report any errors
					errors.AddRange(parseErrors);
				}
				sw.Flush();

				// ensure compacted source for next merge phase
				compacted = sw.ToString();
			}

			if (!this.isMimeSet &&
				!String.IsNullOrEmpty(provider.ContentType) &&
				!String.IsNullOrEmpty(provider.FileExtension))
			{
				this.contentType = provider.ContentType;
				this.fileExtension = provider.FileExtension;
				this.isMimeSet = true;
			}
		}

		protected internal override IList<ParseException> Compact(IResourceBuildHelper helper, string virtualPath, string sourceText, TextWriter writer)
		{
			// these were compacted in the preprocess so just emit
			foreach (string source in this.sources)
			{
				if (String.IsNullOrEmpty(source))
				{
					continue;
				}

				writer.WriteLine(source);
			}

			return null;
		}

		#endregion ResourceCodeProvider Members

		#region Utility Methods

		private static string ScrubResourceName(string resource)
		{
			if (String.IsNullOrEmpty(resource))
			{
				return resource;
			}

			StringBuilder builder = new StringBuilder(resource);
			builder.Replace('/', '.');
			builder.Replace('\\', '.');
			builder.Replace('?', '.');
			builder.Replace('*', '.');
			builder.Replace(':', '.');
			return builder.ToString().TrimStart('.');
		}

		#endregion Utility Methods
	}
}