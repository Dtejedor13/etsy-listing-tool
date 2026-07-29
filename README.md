# Etsy Listing Tool

A desktop utility for generating and maintaining Etsy listing data from local project folders.

The tool is designed for personal use and helps reduce repetitive work when creating or updating Etsy product listings.

---

# Features

* Generate Etsy listing templates from local project folders
* Read product information from JSON files
* Organize product images automatically
* Generate listing titles, descriptions and tags
* Prepare listings for manual review
* Designed to support future Etsy API integration for draft creation and listing updates

---

# Project Structure

The main application is located at:

```
EtsyListingCreatorTool/
└── EtsyBacklogListingGenerator/
    └── Program.cs
```

`Program.cs` is the entry point of the application.

---

# Folder Structure

Each product is stored inside its own directory.

Example:

```
Products/
└── MyFigure/
    ├── info.json
    ├── points.json
    ├── image1.png
    ├── image2.png
    └── image3.png
```

Every folder represents one Etsy listing.

---

# Required Files

## info.json

Contains the general product information.

Example fields include:

* Product name
* Universe
* Original scale
* Available print scales
* Creator
* Resin information
* Product options

---

## points.json

Contains additional listing data used by the generator.

This file is merged together with the information from `info.json` during processing.

---

# Running the Application

1. Clone the repository.

```
git clone https://github.com/Dtejedor13/etsy-listing-tool.git
```

2. Open the solution in Visual Studio.

3. Set

```
EtsyBacklogListingGenerator
```

as the startup project.

4. Build the solution.

5. Run the application.

The application scans the configured base directory for product folders.

For every valid folder containing the required JSON files, listing data is generated.

---

# Workflow

The intended workflow is:

1. Create a new product folder.

2. Add

* info.json
* points.json
* product images

3. Run the generator.

4. Review the generated listing output.

5. Copy the generated content into Etsy manually.

Future versions are intended to support creating Etsy draft listings directly through the Etsy API.

---

# How It Works

For each product folder the application:

* loads `info.json`
* loads `points.json`
* merges the product data
* processes available images
* generates Etsy listing content
* exports the finished listing template

No cloud services are required.

All processing is performed locally.

---

# Technologies

* C#
* .NET
* System.Text.Json
* Local file system
* Etsy API (planned)

---

# Notes

This application is intended for managing a single Etsy shop.

It is not a marketplace automation tool and does not automatically publish listings.

Listing content is always reviewed before being uploaded to Etsy.

---

# License

This project is provided for educational and personal use.

Please ensure that all generated listing content complies with Etsy's Terms of Use.
