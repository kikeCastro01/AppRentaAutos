using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MongoDB.Bson;
using MongoDB.Driver;
namespace prueba;

public partial class ListaAutos : ContentPage
{
	private readonly IMongoCollection<BsonDocument> _collection;
	private string placasSeleccionadas;
	private decimal precioPorDiaSeleccionado;
	public ObservableCollection<Auto> Autos { get; set; }
	public ListaAutos()
	{
		InitializeComponent();

		Autos = new ObservableCollection<Auto>();
		listViewAutos.ItemsSource = Autos;

		//Establecer la conexión con la base de datos MongoDB
	    var client = new MongoClient("mongodb://192.168.1.66:27017");
		var database = client.GetDatabase("RentaAutos");
		_collection = database.GetCollection<BsonDocument>("Autos");

	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Limpiar la lista antes de cargar los datos nuevamente
		Autos.Clear();

		//Cargar los datos en el ListView
		await LoadData();
	}

	private async Task LoadData()
	{
		// Crear el filtro para obtener solo los autos no rentados (Rentado = false)
		var filter = Builders<BsonDocument>.Filter.Eq("Rentado", false);

		// Obtener los documentos de la colección que cumplan con el filtro
		var documents = await _collection.Find(filter).ToListAsync();

		// Iterar sobre cada documento y crear objetos Auto
		foreach (var document in documents)
		{
			var auto = new Auto
			{
				FotoImageSource = ImageFromByteArray(document["Foto"].AsByteArray),
				Placas = document["Placas"].AsString,
				Marca = document["Marca"].AsString,
				Modelo = document["Modelo"].AsString,
				Año = document["Año"].AsInt32,
				Color = document["Color"].AsString,
				PrecioPorDia = document["PrecioPorDia"].ToDecimal(),
			};

			Autos.Add(auto);
		}
		// Notificar a la interfaz de usuario que la colección ha cambiado
		OnPropertyChanged(nameof(Autos));
	}

	private ImageSource ImageFromByteArray(byte[] byteArray)
	{
		if (byteArray == null)
			return null;

		return ImageSource.FromStream(() => new MemoryStream(byteArray));
	}
	public class Auto
	{
		public ImageSource FotoImageSource { get; set; }
		public string Placas { get; set; }
		public string Marca { get; set; }
		public string Modelo { get; set; }
		public int Año { get; set; }
		public string Color { get; set; }
		public decimal PrecioPorDia { get; set; }
	}

	public void MandarDatos()
	{
		var tabbedPage = Application.Current.MainPage as MainPage;
		RentaAutos rentaAutos = tabbedPage.Children[2] as RentaAutos;

		//rentaAutos.RellenarDatos
	}

	private async void btnRentar_Clicked(object sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(placasSeleccionadas) || precioPorDiaSeleccionado == 0)
		{
			await DisplayAlert("Advertencia", "Por favor, seleccione un auto antes de continuar.", "OK");
			return;
		}

		// Mandar los datos a la otra interfaz
		var tabbedPage = Application.Current.MainPage as MainPage;
		var rentaAutos = tabbedPage.Children[2] as RentaAutos;
		rentaAutos.RellenarDatos(placasSeleccionadas, precioPorDiaSeleccionado);
		tabbedPage.CurrentPage = rentaAutos;
	}

	private void auto_ItemSelected(object sender, SelectedItemChangedEventArgs e)
	{
		if (e.SelectedItem == null)
			return;

		var selectedAuto = (Auto)e.SelectedItem;
		placasSeleccionadas = selectedAuto.Placas;
		precioPorDiaSeleccionado = selectedAuto.PrecioPorDia;
	}
}