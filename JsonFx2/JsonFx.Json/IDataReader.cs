#region License
/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2010 Stephen M. McKamey

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

namespace JsonFx.Json
{
	/// <summary>
	/// A common interface for data deserializers
	/// </summary>
	public interface IDataReader
	{
		#region Properties

		/// <summary>
		/// Gets the supported content type of the serialized data
		/// </summary>
		// TODO: should this be IEnumerable<string>?
		string ContentType
		{
			get;
		}

		/// <summary>
		/// Gets the settings used for deserialization
		/// </summary>
		DataReaderSettings Settings
		{
			get;
		}

		#endregion Properties

		#region Methods

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		/// <typeparam name="T">the expected type of the serialized data</typeparam>
		T Deserialize<T>(TextReader input);

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		object Deserialize(TextReader input);

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		/// <param name="targetType">the expected type of the serialized data</param>
		object Deserialize(TextReader input, Type targetType);

		#endregion Methods
	}

	/// <summary>
	/// Provides base implementation of standard deserializers
	/// </summary>
	public abstract class DataReaderBase : IDataReader
	{
		#region Fields

		private readonly DataReaderSettings settings;

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="settings"></param>
		public DataReaderBase(DataReaderSettings settings)
		{
			this.settings = settings;
		}

		#endregion Init

		#region Properties

		/// <summary>
		/// Gets the supported content type of the serialized data
		/// </summary>
		public abstract string ContentType
		{
			get;
		}

		/// <summary>
		/// Gets the settings used for deserialization
		/// </summary>
		public DataReaderSettings Settings
		{
			get { return this.settings; }
		}

		#endregion Properties

		#region Methods

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		/// <typeparam name="T">the expected type of the serialized data</typeparam>
		public virtual T Deserialize<T>(TextReader input)
		{
			object value = this.Deserialize(input, typeof(T));

			return (value is T) ? (T)value : default(T);
		}

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		public virtual object Deserialize(TextReader input)
		{
			return this.Deserialize(input, null);
		}

		/// <summary>
		/// Serializes the data to the given output
		/// </summary>
		/// <param name="input">the input reader</param>
		/// <param name="targetType">the expected type of the serialized data</param>
		public abstract object Deserialize(TextReader input, Type targetType);

		#endregion Methods
	}
}
