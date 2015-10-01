﻿// PROJECT : MsCrmTools.WebResourcesManager
// This project was developed by Tanguy Touzard
// CODEPLEX: http://xrmtoolbox.codeplex.com
// BLOG: http://mscrmtools.blogspot.com

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using Jsbeautifier;
using MsCrmTools.WebResourcesManager.AppCode;
using MsCrmTools.WebResourcesManager.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace MsCrmTools.WebResourcesManager.UserControls
{
    /// <summary>
    /// Control that displays a code web resource
    /// </summary>
    public partial class CodeControl : UserControl, IWebResourceControl
    {
        #region Variables

        //private readonly FindAndReplaceForm findForm = new FindAndReplaceForm();

        /// <summary>
        /// Type of web resource
        /// </summary>
        private readonly Enumerations.WebResourceType innerType;

        /// <summary>
        /// Base64 content of the web resource when loading this control
        /// </summary>
        private readonly string originalContent;

        private FoldingManager foldingManager;

        private bool foldingManagerInstalled;
        private HtmlFoldingStrategy htmlFoldingStrategy;

        /// <summary>
        /// Base64 content of the web resource
        /// </summary>
        private string innerContent;

        private TextEditor textEditor;
        private XmlFoldingStrategy xmlFoldingStrategy;

        #endregion Variables

        #region Delegates

        public delegate void WebResourceUpdatedEventHandler(object sender, WebResourceUpdatedEventArgs e);

        #endregion Delegates

        #region Event Handlers

        public event WebResourceUpdatedEventHandler WebResourceUpdated;

        #endregion Event Handlers

        #region Constructor

        /// <summary>
        /// Initializes a new instance of class CodeControl
        /// </summary>
        /// <param name="content">Base64 content of the web resource</param>
        /// <param name="type">Web resource type</param>
        public CodeControl(string content, Enumerations.WebResourceType type)
        {
            InitializeComponent();

            textEditor = new TextEditor
            {
                ShowLineNumbers = true,
                FontSize = 12,
                //Focusable = true,
                //IsHitTestVisible = true
            };

            var wpfHost = new ElementHost
            {
                Child = textEditor,
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
            };

            Controls.Add(wpfHost);

            if (!string.IsNullOrEmpty(content))
            {
                // Converts base64 content to string
                byte[] b = Convert.FromBase64String(content);
                innerContent = System.Text.Encoding.UTF8.GetString(b);
                originalContent = innerContent;
                innerType = type;
            }
        }

        public void Find(bool replace, IWin32Window owner)
        {
            FindAndReplaceForm.ShowForReplace(textEditor, replace);
        }

        #endregion Constructor

        #region Handlers

        public IEnumerable<NewFolding> CreateBraceFoldings(ITextSource document)
        {
            List<NewFolding> newFoldings = new List<NewFolding>();

            Stack<int> startOffsets = new Stack<int>();
            int lastNewLineOffset = 0;
            char openingBrace = '{';
            char closingBrace = '}';
            for (int i = 0; i < document.TextLength; i++)
            {
                char c = document.GetCharAt(i);
                if (c == openingBrace)
                {
                    startOffsets.Push(i);
                }
                else if (c == closingBrace && startOffsets.Count > 0)
                {
                    int startOffset = startOffsets.Pop();
                    // don't fold if opening and closing brace are on the same line
                    if (startOffset < lastNewLineOffset)
                    {
                        newFoldings.Add(new NewFolding(startOffset, i + 1));
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    lastNewLineOffset = i + 1;
                }
            }
            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }

        private void CodeControl_Load(object sender, EventArgs e)
        {
            try
            {
                textEditor.TextChanged += tecCode_TextChanged;
                textEditor.Text = innerContent;

                switch (innerType)
                {
                    case Enumerations.WebResourceType.Script:
                        {
                            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
                        }
                        break;

                    case Enumerations.WebResourceType.Data:
                        {
                            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
                        }
                        break;

                    case Enumerations.WebResourceType.WebPage:
                        {
                            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
                        }
                        break;

                    case Enumerations.WebResourceType.Css:
                        {
                            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("CSS");
                        }
                        break;

                    case Enumerations.WebResourceType.Xsl:
                        {
                            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
                        }
                        break;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("An error occured while loading this web resource: " + error.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void tecCode_TextChanged(object sender, EventArgs e)
        {
            innerContent = textEditor.Text;
            SendSavedMessage();

            if (foldingManager != null)
            {
                if (xmlFoldingStrategy != null)
                {
                    xmlFoldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                }
                else if (htmlFoldingStrategy != null)
                {
                    htmlFoldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                }
                else
                {
                    foldingManager.UpdateFoldings(CreateBraceFoldings(textEditor.Document), -1);
                }
            }
        }

        #endregion Handlers

        #region Methods

        public string GetBase64WebResourceContent()
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(textEditor.Text);

            return Convert.ToBase64String(bytes);
        }

        public Enumerations.WebResourceType GetWebResourceType()
        {
            return innerType;
        }

        public void MinifyJs()
        {
            try
            {
                textEditor.Text = Yahoo.Yui.Compressor.JavaScriptCompressor.Compress(textEditor.Text, false, true, false, false, 200);
            }
            catch (Exception error)
            {
                MessageBox.Show(ParentForm, "Error while minifying code: " + error.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ReplaceWithNewFile(string filename)
        {
            try
            {
                using (var reader = new StreamReader(filename))
                {
                    innerContent = reader.ReadToEnd();
                }

                CodeControl_Load(null, null);

                SendSavedMessage();
            }
            catch (Exception error)
            {
                MessageBox.Show(ParentForm, "Error while updating file: " + error.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendSavedMessage()
        {
            var wrueArgs = new WebResourceUpdatedEventArgs
                                                       {
                                                           Base64Content = innerContent,
                                                           IsDirty = (innerContent != originalContent),
                                                           Type = innerType
                                                       };

            if (WebResourceUpdated != null)
            {
                WebResourceUpdated(this, wrueArgs);
            }
        }

        #endregion Methods

        internal void Beautify()
        {
            Beautifier b = new Beautifier(new BeautifierOptions
            {
                BraceStyle = BraceStyle.Expand,
                BreakChainedMethods = false,
                EvalCode = true,
                IndentChar = '\t',
                IndentSize = 1,
                IndentWithTabs = true,
                JslintHappy = true,
                KeepArrayIndentation = true,
                KeepFunctionIndentation = true,
                MaxPreserveNewlines = 1,
                PreserveNewlines = true
            });

            textEditor.Text = b.Beautify(textEditor.Text);
        }

        internal void EnableFolding(bool enableFolding)
        {
            if (enableFolding)
            {
                switch (innerType)
                {
                    case Enumerations.WebResourceType.Script:
                    case Enumerations.WebResourceType.Css:
                        {
                            if (!foldingManagerInstalled)
                            {
                                foldingManager = FoldingManager.Install(textEditor.TextArea);
                                foldingManagerInstalled = true;
                            }

                            foldingManager.UpdateFoldings(CreateBraceFoldings(textEditor.Document), -1);
                        }
                        break;

                    case Enumerations.WebResourceType.WebPage:
                        {
                            if (!foldingManagerInstalled)
                            {
                                foldingManager = FoldingManager.Install(textEditor.TextArea);
                                foldingManagerInstalled = true;
                            }

                            htmlFoldingStrategy = new HtmlFoldingStrategy();
                            htmlFoldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                        }
                        break;

                    case Enumerations.WebResourceType.Data:
                    case Enumerations.WebResourceType.Xsl:
                        {
                            if (!foldingManagerInstalled)
                            {
                                foldingManager = FoldingManager.Install(textEditor.TextArea);
                                foldingManagerInstalled = true;
                            }

                            xmlFoldingStrategy = new XmlFoldingStrategy();
                            xmlFoldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
                        }
                        break;
                }
            }
            else
            {
                if (foldingManager != null)
                    foldingManager.Clear();
            }
        }
    }
}