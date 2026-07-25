using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using EtsyBacklogListingGenerator;
using EtsyBacklogListingGenerator.AI;
using EtsyBacklogListingGenerator.Generators;

namespace EtsyListingUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetupControlls()
        {
            cbxOgScale.Items.Clear();
            cbxOgScale.Items.Add("6");
            cbxOgScale.Items.Add("8");
            cbxOgScale.Items.Add("10");

            cbxCreator.Items.Clear();
            cbxCreator.Items.Add("kaidan");
            cbxCreator.Items.Add("samiho");
            cbxCreator.Items.Add("lucas");
            cbxCreator.Items.Add("myanimate");

            cbxScaleOptions.Items.Clear();
            cbxScaleOptions.Items.Add("6,8,10");
            cbxScaleOptions.Items.Add("6,8");
            cbxScaleOptions.Items.Add("6,10");
            cbxScaleOptions.Items.Add("8,10");
            cbxScaleOptions.Items.Add("6");
            cbxScaleOptions.Items.Add("8");
            cbxScaleOptions.Items.Add("10");
        }

        private void ResetControlls()
        {
            txtCharacterName.Text = string.Empty;
            txtInfo.Text = string.Empty;
            txtUniverse.Text = string.Empty;
            txtOriginalSize.Text = string.Empty;
            cbxScaleOptions.SelectedIndex = 0;
            cbxCreator.SelectedIndex = 0;
            cbxOgScale.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupControlls();
            ResetControlls();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var aiManager = new OpenAIManager();
            var tagsGenerator = new TagsGenerator(aiManager);
            var descriptionGenerator = new DescriptionGenerator(aiManager);
            var variationsGenerator = new VariationsGenerator();
            var titleGenerator = new TitleGenerator(aiManager);

            var options = cbxScaleOptions.SelectedItem.ToString()!.Split(",");
            var scales = new List<int>();
            foreach (var option in options)
                scales.Add(Convert.ToInt16(option));
            var json = JsonSerializer.Serialize(scales);

            var listingInfo = new JsonObject()
            {
                ["name"] = txtCharacterName.Text,
                ["universe"] = txtUniverse.Text,
                ["additional_infos"] = txtInfo.Text,
                ["original_size"] = txtOriginalSize.Text,
                ["default_scale"] = Convert.ToInt16(cbxOgScale.SelectedItem.ToString()),
                ["scales"] = JsonSerializer.Deserialize<JsonArray>(json),
                ["creator"] = cbxCreator.SelectedItem.ToString()
            };


            var characterPrompt = Utils.CreateCharacterPrompt(listingInfo);

            // call generators
            var characterName = listingInfo["name"]!.ToString();
            var characterUniverse = listingInfo["universe"]!.ToString();
            var scaleOptions = listingInfo["scales"]!.AsArray();
            var availibleScalesString = Utils.GetAvailibleScalesString(scaleOptions);
            
            var variationString = variationsGenerator.GenerateVariationsString(listingInfo);


            txtTitle.Text = await titleGenerator.GenerateTitleAsync(characterName, characterUniverse);
            txtDescription.Text = await descriptionGenerator.GenerateDescriptionAsync(characterName, characterUniverse, availibleScalesString, listingInfo["creator"]!.ToString());
            txtTags.Text = await tagsGenerator.GenerateTagsAsync(characterPrompt);
        }
    }
}