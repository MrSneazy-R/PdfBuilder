using System;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;

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

    /// <summary>
    /// Canonical content surface passed to reusable components. A container is scoped to the
    /// current document and must not be retained after <c>Compose</c> returns.
    /// </summary>
    public interface IContainer
    {
        IContainer Component(IPdfComponent component);
        IContainer Component<TModel>(IPdfComponent<TModel> component, TModel model);
        IContainer Text(string content, Action<TextElement>? configure = null);
        IContainer Text(string content, string styleName, Action<TextElement>? configure = null);
        IContainer Column(Action<IContainer> configure, float spacing = 8f);
        IContainer Padding(float uniform, Action<IContainer> configure);
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
