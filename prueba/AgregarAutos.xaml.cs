using Microsoft.Maui.ApplicationModel.Communication;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace prueba;

public partial class AgregarAutos : ContentPage
{
	private readonly List<string> placasList = new List<string>();
	private byte[] imagenBytes = null;
	private const string connectionString = "mongodb://192.168.1.66:27017";
	private readonly string databaseName = "RentaAutos";
	private readonly string collectionName = "Autos";

	public AgregarAutos()
	{
		InitializeComponent();
		CargarPlacas();
	}
	private async Task CargarPlacas()
	{
		try
		{
			var placas = await ObtenerPlacasDesdeMongoDB();
			placasList.Clear();
			placasList.AddRange(placas);
			pkrPlacas.ItemsSource = null;
			pkrPlacas.ItemsSource = placasList;
			pkrPlacas.SelectedIndex = -1; // Deseleccionar cualquier elemento seleccionado anteriormente
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Ocurrió un error al cargar las placas: {ex.Message}", "OK");
		}
	}
	private async Task<List<string>> ObtenerPlacasDesdeMongoDB()
	{
		var placas = new List<string>();

		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<Auto>(collectionName);

		var filter = Builders<Auto>.Filter.Eq("Rentado", false);
		var projection = Builders<Auto>.Projection.Include(auto => auto.Placas).Exclude(auto => auto._id);

		using (var cursor = await collection.FindAsync(filter, new FindOptions<Auto, Auto> { Projection = projection }))
		{
			while (await cursor.MoveNextAsync())
			{
				var batch = cursor.Current;
				foreach (var document in batch)
				{
					placas.Add(document.Placas);
				}
			}
		}

		return placas;
	}
	private async Task<Auto> ObtenerAutoDesdeMongoDBPorPlaca(string placa)
	{

		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<Auto>(collectionName);

		var filter = Builders<Auto>.Filter.Eq("Placas", placa);
		var auto = await collection.Find(filter).FirstOrDefaultAsync();

		return auto;
	}
	private async Task ActualizarAutoEnMongoDB(string placa, Auto nuevoAuto)
	{
		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<Auto>(collectionName);

		var filter = Builders<Auto>.Filter.Eq("Placas", placa);
		var update = Builders<Auto>.Update
			.Set("Placas", nuevoAuto.Placas)
			.Set("Marca", nuevoAuto.Marca)
			.Set("Modelo", nuevoAuto.Modelo)
			.Set("Año", nuevoAuto.Año)
			.Set("Color", nuevoAuto.Color)
			.Set("PrecioPorDia", nuevoAuto.PrecioPorDia)
			.Set("Foto", nuevoAuto.Foto);

		await collection.UpdateOneAsync(filter, update);
	}
	public class Auto
	{
		[BsonId]
		public ObjectId _id { get; set; }
		public byte[] Foto { get; set; }
		public string Placas { get; set; }
		public string Marca { get; set; }
		public string Modelo { get; set; }
		public int Año { get; set; }
		public string Color { get; set; }
		public decimal PrecioPorDia { get; set; }
		public bool Rentado { get; set; }
	}

	public class MongoDBManager
	{
		private readonly IMongoCollection<BsonDocument> _collection;

		public MongoDBManager(string connectionString, string databaseName, string collectionName)
		{
			var client = new MongoClient(connectionString);
			var database = client.GetDatabase(databaseName);
			_collection = database.GetCollection<BsonDocument>(collectionName);
		}

		public async Task GuardarDatosEnMongoDB(Auto auto)
		{
			var document = new BsonDocument
			{
				{ "Foto", new BsonBinaryData(auto.Foto) },
				{ "Placas", auto.Placas },
				{ "Marca", auto.Marca },
				{ "Modelo", auto.Modelo },
				{ "Año", auto.Año },
				{ "Color", auto.Color },
				{ "PrecioPorDia", auto.PrecioPorDia },
				{ "Rentado", false }
			};

			await _collection.InsertOneAsync(document);
		}
	}
	private byte[] ConvertirImagenABytes(Stream stream)
	{
		using (MemoryStream ms = new MemoryStream())
		{
			stream.CopyTo(ms);
			return ms.ToArray();
		}
	}

	private async Task GuardarAutoEnMongoDB(Auto auto)
	{
		try
		{
			var mongoManager = new MongoDBManager(connectionString, databaseName, collectionName);
			await mongoManager.GuardarDatosEnMongoDB(auto);
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Ocurrió un error al guardar el auto en MongoDB: {ex.Message}", "OK");
		}
	}

	private async void btnGuardar_Clicked(object sender, EventArgs e)
	{
		try
		{
			// Validar que se haya cargado una imagen
			if (imgAuto.Source == null)
			{
				await DisplayAlert("Error", "Por favor, carga una foto antes de guardar.", "OK");
				return;
			}

			// Convertir la imagen a bytes
			byte[] fotoBytes = ConvertirImagenABytes(await ((StreamImageSource)imgAuto.Source).Stream(CancellationToken.None));

			// Validar que se hayan ingresado todos los campos
			if (string.IsNullOrEmpty(tbPlacas.Text) || string.IsNullOrEmpty(tbMarca.Text) ||
				string.IsNullOrEmpty(tbModelo.Text) || string.IsNullOrEmpty(tbAño.Text) ||
				string.IsNullOrEmpty(tbColor.Text) || string.IsNullOrEmpty(tbPrecio.Text) || imgAuto.Source == null)
			{
				await DisplayAlert("Error", "Por favor, completa todos los campos antes de guardar.", "OK");
				return;
			}
			// Verificar si la placa ya existe en la base de datos
			if (await VerificarPlacaExistente(tbPlacas.Text))
			{
				await DisplayAlert("Error", "La placa ingresada ya existe en la base de datos.", "OK");
				return;
			}

			// Crear un objeto Auto con los datos ingresados
			var nuevoAuto = new Auto
			{
				Foto = fotoBytes,
				Placas = tbPlacas.Text,
				Marca = tbMarca.Text,
				Modelo = tbModelo.Text,
				Año = Convert.ToInt32(tbAño.Text),
				Color = tbColor.Text,
				PrecioPorDia = Convert.ToDecimal(tbPrecio.Text)
			};

			// Guardar en la base de datos MongoDB
			await GuardarAutoEnMongoDB(nuevoAuto);


			await DisplayAlert("Éxito", "Auto guardado correctamente", "OK");


			// Llamar a CargarPlacas después de guardar correctamente
			await CargarPlacas();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Ocurrió un error al guardar el auto: {ex.Message}", "OK");
		}
	}

	private async Task<bool> VerificarPlacaExistente(string placa)
	{
		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<Auto>(collectionName);

		var filter = Builders<Auto>.Filter.Eq("Placas", placa);
		var auto = await collection.Find(filter).FirstOrDefaultAsync();

		return auto != null; // Si hay un auto con la misma placa, retornamos true
	}

	private async void btnCargarFoto_Clicked(object sender, EventArgs e)
	{
		try
		{
			// Abrir el selector de imágenes del dispositivo
			var resultado = await MediaPicker.PickPhotoAsync();

			if (resultado != null)
			{
				// Asignar la imagen seleccionada al control de Image
				imgAuto.Source = ImageSource.FromStream(() => resultado.OpenReadAsync().Result);
			}
		}
		catch (Exception ex)
		{
			// Manejar cualquier excepción que pueda ocurrir durante la selección de la foto
			await DisplayAlert("Error", $"Ocurrió un error al cargar la foto: {ex.Message}", "OK");
		}
	}

	private async void pkrPlacas_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (pkrPlacas.SelectedIndex >= 0)
		{
			var placaSeleccionada = placasList[pkrPlacas.SelectedIndex];
			var auto = await ObtenerAutoDesdeMongoDBPorPlaca(placaSeleccionada);

			if (auto != null)
			{
				// Rellenar los campos con los datos del auto
				tbPlacas.Text = auto.Placas;
				tbMarca.Text = auto.Marca;
				tbModelo.Text = auto.Modelo;
				tbAño.Text = auto.Año.ToString();
				tbColor.Text = auto.Color;
				tbPrecio.Text = auto.PrecioPorDia.ToString();
				imgAuto.Source = ImageSource.FromStream(() => new MemoryStream(auto.Foto));
				imagenBytes = auto.Foto;
			}

			// Habilitar la edición en el Picker cuando se selecciona un elemento
			tbPlacas.IsEnabled = false;
		}
		else
		{

			// Deshabilitar la edición en el Picker cuando no hay ningún elemento seleccionado
			tbPlacas.IsEnabled = true;
		}
	}

	private async void btnModificar_Clicked(object sender, EventArgs e)
	{
		try
		{
			// Validar que se haya seleccionado una placa
			if (pkrPlacas.SelectedIndex < 0)
			{
				await DisplayAlert("Error", "Por favor, seleccione una placa antes de modificar.", "OK");
				return;
			}

			// Obtener la placa seleccionada
			var placaSeleccionada = placasList[pkrPlacas.SelectedIndex];

			// Validar que la placa no haya cambiado
			if (placaSeleccionada != tbPlacas.Text)
			{
				// Verificar si la nueva placa ya existe en la base de datos
				if (await VerificarPlacaExistente(tbPlacas.Text))
				{
					await DisplayAlert("Error", "La nueva placa ingresada ya existe en la base de datos.", "OK");
					return;
				}
			}

			// Obtener los nuevos datos del auto
			var nuevoAuto = new Auto
			{
				_id = ObjectId.GenerateNewId(), // Generar un nuevo ObjectId para cada documento
				Placas = tbPlacas.Text,
				Marca = tbMarca.Text,
				Modelo = tbModelo.Text,
				Año = Convert.ToInt32(tbAño.Text),
				Color = tbColor.Text,
				PrecioPorDia = Convert.ToDecimal(tbPrecio.Text),
				Foto = ConvertirImagenABytes(await ((StreamImageSource)imgAuto.Source).Stream(CancellationToken.None)) // Aquí asignas la nueva imagen
			};

			// Actualizar el auto en la base de datos MongoDB
			await ActualizarAutoEnMongoDB(placaSeleccionada, nuevoAuto);

			await DisplayAlert("Éxito", "Auto actualizado correctamente", "OK");

			// Llamar a CargarPlacas después de modificar correctamente
			await CargarPlacas();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Ocurrió un error al modificar el auto: {ex.Message}", "OK");
		}
	}
}