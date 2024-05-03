using Sandbox;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace SandbankDatabase;

internal class MockFileIOProvider : IFileIOProvider
{
	private ConcurrentDictionary<string, MockFile> _fileSystem = new();

	public string ReadAllText( string file )
	{
		var f = _fileSystem[file];

		if ( f.FileType == MockFileType.File )
			return f.Contents;

		return null;
	}

	public void WriteAllText( string file, string text )
	{
		_fileSystem[file] = new MockFile
		{
			Contents = text,
			FileType = MockFileType.File
		};
	}

	public void CreateDirectory( string directory )
	{
		_fileSystem[directory] = new MockFile
		{
			FileType = MockFileType.Directory
		};
	}

	public void DeleteDirectory( string directory, bool recursive = false )
	{
		if ( recursive )
			throw new Exception( "not supported" );

		_fileSystem.Remove( directory, out _ );
	}

	public bool DirectoryExists( string directory )
	{
		return true;
	}

	public IEnumerable<string> FindFile( string folder, string pattern = "*", bool recursive = false )
	{
		if ( recursive )
			throw new Exception( "not supported" );

		// This is buggy but it'll do.
		var files = _fileSystem.Where( x => x.Value.FileType == MockFileType.File
			&& x.Key.StartsWith( folder )
			&& Regex.IsMatch( x.Key, pattern ) );

		return files.Select(x => x.Key);
	}

	public IEnumerable<string> FindDirectory( string folder, string pattern = "*", bool recursive = false )
	{
		if ( recursive )
			throw new Exception( "not supported" );

		// This is buggy but it'll do.
		var files = _fileSystem.Where( x => x.Value.FileType == MockFileType.Directory
			&& x.Key.StartsWith( folder )
			&& Regex.IsMatch( x.Key, pattern ) );

		return files.Select( x => x.Key );
	}

	public void DeleteFile( string file )
	{
		_fileSystem.Remove( file, out _ );
	}

	private struct MockFile
	{
		public MockFileType FileType;
		public string Contents;
	}

	private enum MockFileType
	{
		File,
		Directory
	}
}
