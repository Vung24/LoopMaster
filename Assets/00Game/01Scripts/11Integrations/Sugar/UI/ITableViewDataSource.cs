namespace Sugar.UI
{
	public interface ITableViewDataSource
	{
		int GetNumOfSections(TableView tableView);
		int GetNumOfCellsInSection(TableView tableView, int section);
		float GetHeightOfCell(TableView tableView, int section, int row);
		TableViewCell GetTableViewCell(TableView tableView, int section, int row);
	}
}
