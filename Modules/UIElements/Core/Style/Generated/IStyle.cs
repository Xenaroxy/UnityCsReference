// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

/******************************************************************************/
//
//                             DO NOT MODIFY
//          This file has been generated by the UIElementsGenerator tool
//              See InlineStyleInterfaceCsGenerator class for details
//
/******************************************************************************/
namespace UnityEngine.UIElements
{
    /// <summary>
    /// This interface provides access to a VisualElement inline style data.
    /// </summary>
    /// <remarks>
    /// Reading properties from this object will read from the inline style data for this element.
    /// To read the style data computed for the element use <see cref="IStyle"/> interface.
    /// Writing to a property will mask the value coming from USS with the provided value however other properties will still match the values from USS.
    /// </remarks>
    public partial interface IStyle
    {
        /// <summary>
        /// Alignment of the whole area of children on the cross axis if they span over multiple lines in this container.
        /// </summary>
        StyleEnum<Align> alignContent { get; set; }
        /// <summary>
        /// Alignment of children on the cross axis of this container.
        /// </summary>
        StyleEnum<Align> alignItems { get; set; }
        /// <summary>
        /// Similar to align-items, but only for this specific element.
        /// </summary>
        StyleEnum<Align> alignSelf { get; set; }
        /// <summary>
        /// Background color to paint in the element's box.
        /// </summary>
        StyleColor backgroundColor { get; set; }
        /// <summary>
        /// Background image to paint in the element's box.
        /// </summary>
        StyleBackground backgroundImage { get; set; }
        /// <summary>
        /// Background image x position value.
        /// </summary>
        StyleBackgroundPosition backgroundPositionX { get; set; }
        /// <summary>
        /// Background image y position value.
        /// </summary>
        StyleBackgroundPosition backgroundPositionY { get; set; }
        /// <summary>
        /// Background image repeat value.
        /// </summary>
        StyleBackgroundRepeat backgroundRepeat { get; set; }
        /// <summary>
        /// Background image size value. Transitions are fully supported only when using size in pixels or percentages, such as pixel-to-pixel or percentage-to-percentage transitions.
        /// </summary>
        StyleBackgroundSize backgroundSize { get; set; }
        /// <summary>
        /// Color of the element's bottom border.
        /// </summary>
        StyleColor borderBottomColor { get; set; }
        /// <summary>
        /// The radius of the bottom-left corner when a rounded rectangle is drawn in the element's box.
        /// </summary>
        StyleLength borderBottomLeftRadius { get; set; }
        /// <summary>
        /// The radius of the bottom-right corner when a rounded rectangle is drawn in the element's box.
        /// </summary>
        StyleLength borderBottomRightRadius { get; set; }
        /// <summary>
        /// Space reserved for the bottom edge of the border during the layout phase.
        /// </summary>
        StyleFloat borderBottomWidth { get; set; }
        /// <summary>
        /// Color of the element's left border.
        /// </summary>
        StyleColor borderLeftColor { get; set; }
        /// <summary>
        /// Space reserved for the left edge of the border during the layout phase.
        /// </summary>
        StyleFloat borderLeftWidth { get; set; }
        /// <summary>
        /// Color of the element's right border.
        /// </summary>
        StyleColor borderRightColor { get; set; }
        /// <summary>
        /// Space reserved for the right edge of the border during the layout phase.
        /// </summary>
        StyleFloat borderRightWidth { get; set; }
        /// <summary>
        /// Color of the element's top border.
        /// </summary>
        StyleColor borderTopColor { get; set; }
        /// <summary>
        /// The radius of the top-left corner when a rounded rectangle is drawn in the element's box.
        /// </summary>
        StyleLength borderTopLeftRadius { get; set; }
        /// <summary>
        /// The radius of the top-right corner when a rounded rectangle is drawn in the element's box.
        /// </summary>
        StyleLength borderTopRightRadius { get; set; }
        /// <summary>
        /// Space reserved for the top edge of the border during the layout phase.
        /// </summary>
        StyleFloat borderTopWidth { get; set; }
        /// <summary>
        /// Bottom distance from the element's box during layout.
        /// </summary>
        StyleLength bottom { get; set; }
        /// <summary>
        /// Color to use when drawing the text of an element.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleColor color { get; set; }
        /// <summary>
        /// Mouse cursor to display when the mouse pointer is over an element.
        /// </summary>
        StyleCursor cursor { get; set; }
        /// <summary>
        /// Defines how an element is displayed in the layout.
        /// </summary>
        /// <remarks>
        /// Unlike the visibility property, this property affects the layout of the element.
        /// This is a convenient way to hide an element without removing it from the hierarchy
        /// (when using the <see cref="DisplayStyle.None"/>).
        /// 
        /// Elements with a display style of <see cref="DisplayStyle.None"/> are ignored by pointer events
        /// and by <see cref="IPanel.Pick"/>.
        /// </remarks>
        StyleEnum<DisplayStyle> display { get; set; }
        /// <summary>
        /// Initial main size of a flex item, on the main flex axis. The final layout might be smaller or larger, according to the flex shrinking and growing determined by the other flex properties.
        /// </summary>
        StyleLength flexBasis { get; set; }
        /// <summary>
        /// Direction of the main axis to layout children in a container.
        /// </summary>
        StyleEnum<FlexDirection> flexDirection { get; set; }
        /// <summary>
        /// Specifies how the item will grow relative to the rest of the flexible items inside the same container.
        /// </summary>
        StyleFloat flexGrow { get; set; }
        /// <summary>
        /// Specifies how the item will shrink relative to the rest of the flexible items inside the same container.
        /// </summary>
        StyleFloat flexShrink { get; set; }
        /// <summary>
        /// Placement of children over multiple lines if not enough space is available in this container.
        /// </summary>
        StyleEnum<Wrap> flexWrap { get; set; }
        /// <summary>
        /// Font size to draw the element's text, specified in point size.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleLength fontSize { get; set; }
        /// <summary>
        /// Fixed height of an element for the layout.
        /// </summary>
        StyleLength height { get; set; }
        /// <summary>
        /// Justification of children on the main axis of this container.
        /// </summary>
        StyleEnum<Justify> justifyContent { get; set; }
        /// <summary>
        /// Left distance from the element's box during layout.
        /// </summary>
        StyleLength left { get; set; }
        /// <summary>
        /// Increases or decreases the space between characters.
        /// </summary>
        StyleLength letterSpacing { get; set; }
        /// <summary>
        /// Space reserved for the bottom edge of the margin during the layout phase.
        /// </summary>
        StyleLength marginBottom { get; set; }
        /// <summary>
        /// Space reserved for the left edge of the margin during the layout phase.
        /// </summary>
        StyleLength marginLeft { get; set; }
        /// <summary>
        /// Space reserved for the right edge of the margin during the layout phase.
        /// </summary>
        StyleLength marginRight { get; set; }
        /// <summary>
        /// Space reserved for the top edge of the margin during the layout phase.
        /// </summary>
        StyleLength marginTop { get; set; }
        /// <summary>
        /// Maximum height for an element, when it is flexible or measures its own size.
        /// </summary>
        StyleLength maxHeight { get; set; }
        /// <summary>
        /// Maximum width for an element, when it is flexible or measures its own size.
        /// </summary>
        StyleLength maxWidth { get; set; }
        /// <summary>
        /// Minimum height for an element, when it is flexible or measures its own size.
        /// </summary>
        StyleLength minHeight { get; set; }
        /// <summary>
        /// Minimum width for an element, when it is flexible or measures its own size.
        /// </summary>
        StyleLength minWidth { get; set; }
        /// <summary>
        /// Specifies the transparency of an element and of its children.
        /// </summary>
        /// <remarks>
        /// The opacity can be between 0.0 and 1.0. The lower value, the more transparent.
        /// </remarks>
        StyleFloat opacity { get; set; }
        /// <summary>
        /// How a container behaves if its content overflows its own box.
        /// </summary>
        StyleEnum<Overflow> overflow { get; set; }
        /// <summary>
        /// Space reserved for the bottom edge of the padding during the layout phase.
        /// </summary>
        StyleLength paddingBottom { get; set; }
        /// <summary>
        /// Space reserved for the left edge of the padding during the layout phase.
        /// </summary>
        StyleLength paddingLeft { get; set; }
        /// <summary>
        /// Space reserved for the right edge of the padding during the layout phase.
        /// </summary>
        StyleLength paddingRight { get; set; }
        /// <summary>
        /// Space reserved for the top edge of the padding during the layout phase.
        /// </summary>
        StyleLength paddingTop { get; set; }
        /// <summary>
        /// Element's positioning in its parent container.
        /// </summary>
        /// <remarks>
        /// This property is used in conjunction with left, top, right and bottom properties.
        /// </remarks>
        StyleEnum<Position> position { get; set; }
        /// <summary>
        /// Right distance from the element's box during layout.
        /// </summary>
        StyleLength right { get; set; }
        /// <summary>
        /// A rotation transformation.
        /// </summary>
        StyleRotate rotate { get; set; }
        /// <summary>
        /// A scaling transformation.
        /// </summary>
        StyleScale scale { get; set; }
        /// <summary>
        /// The element's text overflow mode.
        /// </summary>
        StyleEnum<TextOverflow> textOverflow { get; set; }
        /// <summary>
        /// Drop shadow of the text.
        /// </summary>
        StyleTextShadow textShadow { get; set; }
        /// <summary>
        /// Top distance from the element's box during layout.
        /// </summary>
        StyleLength top { get; set; }
        /// <summary>
        /// The transformation origin is the point around which a transformation is applied.
        /// </summary>
        StyleTransformOrigin transformOrigin { get; set; }
        /// <summary>
        /// Duration to wait before starting a property's transition effect when its value changes.
        /// </summary>
        StyleList<TimeValue> transitionDelay { get; set; }
        /// <summary>
        /// Time a transition animation should take to complete.
        /// </summary>
        StyleList<TimeValue> transitionDuration { get; set; }
        /// <summary>
        /// Properties to which a transition effect should be applied.
        /// </summary>
        StyleList<StylePropertyName> transitionProperty { get; set; }
        /// <summary>
        /// Determines how intermediate values are calculated for properties modified by a transition effect.
        /// </summary>
        StyleList<EasingFunction> transitionTimingFunction { get; set; }
        /// <summary>
        /// A translate transformation.
        /// </summary>
        StyleTranslate translate { get; set; }
        /// <summary>
        /// Tinting color for the element's backgroundImage.
        /// </summary>
        StyleColor unityBackgroundImageTintColor { get; set; }
        /// <summary>
        /// TextElement editor rendering mode.
        /// </summary>
        StyleEnum<EditorTextRenderingMode> unityEditorTextRenderingMode { get; set; }
        /// <summary>
        /// Font to draw the element's text, defined as a Font object.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleFont unityFont { get; set; }
        /// <summary>
        /// Font to draw the element's text, defined as a FontDefinition structure. It takes precedence over `-unity-font`.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleFontDefinition unityFontDefinition { get; set; }
        /// <summary>
        /// Font style and weight (normal, bold, italic) to draw the element's text.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        /// <summary>
        /// Specifies which box the element content is clipped against.
        /// </summary>
        StyleEnum<OverflowClipBox> unityOverflowClipBox { get; set; }
        /// <summary>
        /// Increases or decreases the space between paragraphs.
        /// </summary>
        StyleLength unityParagraphSpacing { get; set; }
        /// <summary>
        /// Size of the 9-slice's bottom edge when painting an element's background image.
        /// </summary>
        StyleInt unitySliceBottom { get; set; }
        /// <summary>
        /// Size of the 9-slice's left edge when painting an element's background image.
        /// </summary>
        StyleInt unitySliceLeft { get; set; }
        /// <summary>
        /// Size of the 9-slice's right edge when painting an element's background image.
        /// </summary>
        StyleInt unitySliceRight { get; set; }
        /// <summary>
        /// Scale applied to an element's slices.
        /// </summary>
        StyleFloat unitySliceScale { get; set; }
        /// <summary>
        /// Size of the 9-slice's top edge when painting an element's background image.
        /// </summary>
        StyleInt unitySliceTop { get; set; }
        /// <summary>
        /// Specifies the type of sclicing.
        /// </summary>
        StyleEnum<SliceType> unitySliceType { get; set; }
        /// <summary>
        /// Horizontal and vertical text alignment in the element's box.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleEnum<TextAnchor> unityTextAlign { get; set; }
        /// <summary>
        /// Switches between Unity's standard and advanced text generator
        /// </summary>
        /// <remarks>
        /// The advanced text generator supports comprehensive Unicode and text shaping for various languages and scripts, including RTL languages. However, it's currently in development and may not have full feature parity with the standard generator. This property is inherited by default and affects text rendering capabilities.
        /// </remarks>
        StyleEnum<TextGeneratorType> unityTextGenerator { get; set; }
        /// <summary>
        /// Outline color of the text.
        /// </summary>
        StyleColor unityTextOutlineColor { get; set; }
        /// <summary>
        /// Outline width of the text.
        /// </summary>
        StyleFloat unityTextOutlineWidth { get; set; }
        /// <summary>
        /// The element's text overflow position.
        /// </summary>
        StyleEnum<TextOverflowPosition> unityTextOverflowPosition { get; set; }
        /// <summary>
        /// Specifies whether or not an element is visible.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleEnum<Visibility> visibility { get; set; }
        /// <summary>
        /// Word wrap over multiple lines if not enough space is available to draw the text of an element.
        /// </summary>
        /// <remarks>
        /// This property is inherited by default.
        /// </remarks>
        StyleEnum<WhiteSpace> whiteSpace { get; set; }
        /// <summary>
        /// Fixed width of an element for the layout.
        /// </summary>
        StyleLength width { get; set; }
        /// <summary>
        /// Increases or decreases the space between words.
        /// </summary>
        StyleLength wordSpacing { get; set; }
    }
}
