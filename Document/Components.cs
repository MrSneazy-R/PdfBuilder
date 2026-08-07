using System;

namespace PdfBuilder.Document
{
    /// <summary>Reusable, model-independent document content.</summary>
    public interface IPdfComponent
    {
        /// <summary>Composes this component into the supplied container.</summary>
        void Compose(IContainer container);
    }

    /// <summary>Reusable document content bound to a strongly typed model.</summary>
    /// <typeparam name="TModel">The immutable input model type.</typeparam>
    public interface IPdfComponent<in TModel>
    {
        /// <summary>Composes this component using the supplied model.</summary>
        void Compose(IContainer container, TModel model);
    }

    /// <summary>Thrown when a reusable component cannot be composed safely.</summary>
    public sealed class PdfComponentCompositionException : InvalidOperationException
    {
        internal PdfComponentCompositionException(string message, string componentPath, Exception? innerException = null)
            : base(message, innerException)
        {
            ComponentPath = componentPath;
        }

        /// <summary>Component type path active when composition failed.</summary>
        public string ComponentPath { get; }
    }
}
