using Microsoft.Maui.Controls;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace prueba;

public partial class RentaAutos : ContentPage
{
	private const string connectionString = "mongodb://192.168.59.157:27017";
	private readonly string databaseName = "RentaAutos";
	private readonly string collectionName = "Autos";

	public RentaAutos()
	{
		InitializeComponent();
		tbPrecioRenta.TextChanged += CalcularPrecioTotal;
		dpFechaSalida.DateSelected += CalcularPrecioTotal;
		dpFechaEntrega.DateSelected += CalcularPrecioTotal;
		tbPlacasRenta.IsReadOnly = true;
		tbPrecioRenta.IsReadOnly = true;
		tbPrecioTotal.IsReadOnly = true;
	}

	public void RellenarDatos(string placas, decimal precioPorDia)
	{
		tbPlacasRenta.Text = placas;
		tbPrecioRenta.Text = precioPorDia.ToString();
	}
	private void CalcularPrecioTotal(object sender, EventArgs e)
	{
		// Verificar si se ha ingresado un precio y se han seleccionado ambas fechas
		if (double.TryParse(tbPrecioRenta.Text, out double precioPorDia) &&
			dpFechaSalida.Date != default && dpFechaEntrega.Date != default &&
			dpFechaEntrega.Date >= dpFechaSalida.Date) // Verificar que la fecha de entrega sea mayor o igual que la fecha de salida
		{
			// Calcular la diferencia de días entre la fecha de salida y entrega
			TimeSpan diferencia = dpFechaEntrega.Date - dpFechaSalida.Date;
			int diasRenta = (int)diferencia.TotalDays;

			// Calcular el precio total de la renta
			double precioTotal = precioPorDia * diasRenta;

			// Asignar el precio total al Entry tbPrecioTotal
			tbPrecioTotal.Text = precioTotal.ToString();
		}
		else
		{
			// Si falta alguno de los valores o la fecha de entrega es anterior a la fecha de salida, limpiar el Entry tbPrecioTotal
			tbPrecioTotal.Text = string.Empty;
		}
	}
	private async void btnRentar_Clicked(object sender, EventArgs e)
	{
		string cliente = tbCliente.Text;
		string telefono = tbTelefono.Text;
		string placas = tbPlacasRenta.Text;
		string precio = tbPrecioRenta.Text;
		DateTime fechaSalida = dpFechaSalida.Date;
		DateTime fechaEntrega = dpFechaEntrega.Date;

		// Validar que todos los campos estén completos
		if (string.IsNullOrWhiteSpace(cliente) || string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(placas) ||
			string.IsNullOrWhiteSpace(precio))
		{
			DisplayAlert("Error", "Por favor, complete todos los campos.", "OK");
			return; // Evita continuar con la operación de guardar
		}

		// Método para verificar si una cadena contiene solo números
		bool EsNumero(string valor)
		{
			foreach (char c in valor)
			{
				if (!char.IsDigit(c))
				{
					return false;
				}
			}
			return true;
		}

		// Validar que el teléfono solo contenga dígitos y tenga una longitud máxima de 10
		if (!EsNumero(telefono))
		{
			DisplayAlert("Error", "El número de teléfono no es válido.", "OK");
			return; // Evita continuar con la operación de guardar
		}
		else if (telefono.Length != 10)
		{
			DisplayAlert("Error", "El número de teléfono no es válido.", "OK");
			return; // Evita continuar con la operación de guardar
		}

		var placa = tbPlacasRenta.Text;

		// Verificar si la placa existe en la base de datos antes de rentar
		if (!await VerificarPlacaExistente(placa))
		{
			DisplayAlert("Error", "La placa del auto no existe en la base de datos.", "OK");
			return;
		}

		// Actualizar el auto en MongoDB
		await RentarAuto(placa);

		await DisplayAlert("Éxito", "Auto rentado correctamente", "OK");
	}

	private async Task RentarAuto(string placa)
	{
		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<BsonDocument>(collectionName);

		var filter = Builders<BsonDocument>.Filter.Eq("Placas", placa);
		var update = Builders<BsonDocument>.Update.Set("Rentado", true);

		await collection.UpdateOneAsync(filter, update);
	}

	private async Task<bool> VerificarPlacaExistente(string placa)
	{
		var client = new MongoClient(connectionString);
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<BsonDocument>(collectionName);

		var filter = Builders<BsonDocument>.Filter.Eq("Placas", placa);
		var result = await collection.Find(filter).FirstOrDefaultAsync();

		return result != null;
	}
}