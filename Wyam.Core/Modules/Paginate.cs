﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Common;
using Wyam.Common.Documents;
using Wyam.Common.Modules;
using Wyam.Common.Pipelines;
using Wyam.Core.Documents;

namespace Wyam.Core.Modules
{
    /// <summary>
    /// Splits a sequence of documents into multiple pages.
    /// </summary>
    /// <remarks>
    /// Each input document is then cloned for each page and metadata related 
    /// to the pages, including the sequence of documents for each page, 
    /// is added to each clone.
    /// </remarks>
    /// <example>
    /// If your input document is a Razor template for a blog archive, you can use 
    /// Paginate to get pages of 10 blog posts each. If you have 50 blog posts, the 
    /// result of the Paginate module will be 5 copies of your input archive template, 
    /// one for each page. Your configuration file might look something like this:
    /// <code>
    /// Pipelines.Add("Posts",
    ///     ReadFiles("*.md"),
    ///     Markdown(),
    ///     WriteFiles("html")
    /// );
    ///
    /// Pipelines.Add("Archive",
    ///     ReadFiles("archive.cshtml"),
    ///     Paginate(10,
    ///         Documents("Posts")
    ///     ),
    ///     Razor(),
    ///     WriteFiles(string.Format("archive-{0}.html", @doc["CurrentPage"]))
    /// );
    /// </code>
    /// </example>
    /// <metadata name="PageDocuments">An IEnumerable&lt;IDocument&gt; containing all the documents for the current page.</metadata>
    /// <metadata name="CurrentPage">The index of the current page (1 based).</metadata>
    /// <metadata name="TotalPages">The total number of pages.</metadata>
    /// <metadata name="HasNextPage">Whether there is another page after this one.</metadata>
    /// <metadata name="HasPreviousPage">Whether there is another page before this one.</metadata>
    /// <category>Control</category>
    public class Paginate : IModule
    {
        private readonly int _pageSize;
        private readonly IModule[] _modules;

        /// <summary>
        /// Partitions the result of the specified modules into the specified number of pages. The 
        /// input documents to Paginate are used as the initial input documents to the specified modules.
        /// </summary>
        /// <param name="pageSize">The number of documents on each page.</param>
        /// <param name="modules">The modules to execute to get the documents to page.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public Paginate(int pageSize, params IModule[] modules)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentException(nameof(pageSize));
            }
            _pageSize = pageSize;
            _modules = modules;
        }

        public IEnumerable<IDocument> Execute(IReadOnlyList<IDocument> inputs, IExecutionContext context)
        {
            ImmutableArray<ImmutableArray<IDocument>> partitions 
                = Partition(context.Execute(_modules, inputs), _pageSize).ToImmutableArray();
            if (partitions.Length == 0)
            {
                return inputs;
            }
            return inputs.SelectMany(input =>
            {
                return partitions.Select((x, i) => input.Clone(
                    new Dictionary<string, object>
                    {
                        {MetadataKeys.PageDocuments, partitions[i]},
                        {MetadataKeys.CurrentPage, i + 1},
                        {MetadataKeys.TotalPages, partitions.Length},
                        {MetadataKeys.HasNextPage, partitions.Length > i + 1},
                        {MetadataKeys.HasPreviousPage, i != 0}
                    })
                );
            });
        }

        // Interesting discussion of partitioning at
        // http://stackoverflow.com/questions/419019/split-list-into-sublists-with-linq
        // Note that this implementation won't work for very long sequences because it enumerates twice per chunk
        private static IEnumerable<ImmutableArray<T>> Partition<T>(IReadOnlyList<T> source, int size)
        {
            int pos = 0;
            while (source.Skip(pos).Any())
            {
                yield return source.Skip(pos).Take(size).ToImmutableArray();
                pos += size;
            }
        }
    }
}
